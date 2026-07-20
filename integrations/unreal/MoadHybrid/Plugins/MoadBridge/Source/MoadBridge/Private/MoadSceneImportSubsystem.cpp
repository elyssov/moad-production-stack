#include "MoadSceneImportSubsystem.h"

#include "Components/SplineComponent.h"
#include "Dom/JsonObject.h"
#include "Editor.h"
#include "EngineUtils.h"
#include "HAL/FileManager.h"
#include "MoadTrackActor.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

namespace
{
bool ReadPoint(const TSharedPtr<FJsonObject>& Object, FVector& OutPoint)
{
    double X = 0.0;
    double Y = 0.0;
    double Z = 0.0;
    if (!Object.IsValid()
        || !Object->TryGetNumberField(TEXT("x_cm"), X)
        || !Object->TryGetNumberField(TEXT("y_cm"), Y)
        || !Object->TryGetNumberField(TEXT("z_cm"), Z))
    {
        return false;
    }
    OutPoint = FVector(X, Y, Z);
    return true;
}

bool PopulateSpline(USplineComponent& Spline, const TArray<TSharedPtr<FJsonValue>>& PointValues, FString& Error)
{
    if (PointValues.Num() < 2)
    {
        Error = TEXT("Spline needs at least two points");
        return false;
    }
    Spline.ClearSplinePoints(false);
    for (const TSharedPtr<FJsonValue>& Value : PointValues)
    {
        FVector Point;
        if (!ReadPoint(Value->AsObject(), Point))
        {
            Error = TEXT("Spline contains an invalid point");
            return false;
        }
        Spline.AddSplinePoint(Point, ESplineCoordinateSpace::World, false);
    }
    for (int32 Index = 0; Index < PointValues.Num(); ++Index)
    {
        Spline.SetSplinePointType(Index, ESplinePointType::Linear, false);
    }
    Spline.UpdateSpline();
    return true;
}

FString RequiredString(const TSharedPtr<FJsonObject>& Object, const TCHAR* Field)
{
    FString Value;
    return Object.IsValid() && Object->TryGetStringField(Field, Value) ? Value : FString();
}
}

FMoadImportResult UMoadSceneImportSubsystem::ImportSceneContract(
    const FString& AbsoluteSceneJsonPath,
    bool bReplaceGeneratedActors)
{
    FMoadImportResult Result;
    FString JsonText;
    if (!FFileHelper::LoadFileToString(JsonText, *AbsoluteSceneJsonPath))
    {
        Result.Message = FString::Printf(TEXT("Cannot read scene contract: %s"), *AbsoluteSceneJsonPath);
        return Result;
    }

    TSharedPtr<FJsonObject> Root;
    const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JsonText);
    if (!FJsonSerializer::Deserialize(Reader, Root) || !Root.IsValid())
    {
        Result.Message = TEXT("Scene contract is not valid JSON");
        return Result;
    }

    FString Format;
    double Version = 0.0;
    if (!Root->TryGetStringField(TEXT("format"), Format)
        || Format != TEXT("moad-unreal-scene")
        || !Root->TryGetNumberField(TEXT("version"), Version)
        || Version != 1.0)
    {
        Result.Message = TEXT("Unsupported MoAD Unreal scene contract");
        return Result;
    }

    UWorld* World = GEditor ? GEditor->GetEditorWorldContext().World() : nullptr;
    if (!World)
    {
        Result.Message = TEXT("No active editor world");
        return Result;
    }

    if (bReplaceGeneratedActors)
    {
        TArray<AActor*> ToDestroy;
        for (TActorIterator<AActor> It(World); It; ++It)
        {
            if (It->ActorHasTag(TEXT("MoadGenerated")))
            {
                ToDestroy.Add(*It);
            }
        }
        for (AActor* Actor : ToDestroy)
        {
            World->EditorDestroyActor(Actor, true);
        }
    }

    const TArray<TSharedPtr<FJsonValue>>* Supports = nullptr;
    const TArray<TSharedPtr<FJsonValue>>* Transitions = nullptr;
    if (!Root->TryGetArrayField(TEXT("supports"), Supports)
        || !Root->TryGetArrayField(TEXT("transitions"), Transitions))
    {
        Result.Message = TEXT("Scene contract has no supports or transitions arrays");
        return Result;
    }

    auto SpawnTrack = [&](const TSharedPtr<FJsonObject>& Object, bool bTransition, FString& Error) -> bool
    {
        const FString Id = RequiredString(Object, TEXT("id"));
        const FString Lane = RequiredString(Object, bTransition ? TEXT("from_lane_id") : TEXT("lane_id"));
        const TArray<TSharedPtr<FJsonValue>>* Points = nullptr;
        if (Id.IsEmpty() || Lane.IsEmpty() || !Object->TryGetArrayField(TEXT("points"), Points))
        {
            Error = TEXT("Track is missing id, lane, or points");
            return false;
        }
        // ContractId is the stable identity. Let Unreal allocate the transient object
        // name so a replace-import cannot collide with actors pending destruction.
        AMoadTrackActor* Actor = World->SpawnActor<AMoadTrackActor>(AMoadTrackActor::StaticClass(), FTransform::Identity);
        if (!Actor)
        {
            Error = FString::Printf(TEXT("Could not spawn track %s"), *Id);
            return false;
        }
        Actor->ContractId = Id;
        Actor->LaneId = Lane;
        Actor->bIsTransition = bTransition;
        Actor->SetActorLabel(FString::Printf(TEXT("MOAD %s: %s"), bTransition ? TEXT("Transition") : TEXT("Support"), *Id));
        if (bTransition)
        {
            Actor->TargetLaneId = RequiredString(Object, TEXT("to_lane_id"));
            Actor->TraversalType = RequiredString(Object, TEXT("type"));
        }
        else
        {
            Actor->TraversalType = RequiredString(Object, TEXT("traversal_mode"));
        }
        if (!PopulateSpline(*Actor->Spline, *Points, Error))
        {
            World->EditorDestroyActor(Actor, true);
            return false;
        }
        return true;
    };

    FString Error;
    for (const TSharedPtr<FJsonValue>& Value : *Supports)
    {
        if (!SpawnTrack(Value->AsObject(), false, Error))
        {
            Result.Message = Error;
            return Result;
        }
        ++Result.SupportCount;
    }
    for (const TSharedPtr<FJsonValue>& Value : *Transitions)
    {
        if (!SpawnTrack(Value->AsObject(), true, Error))
        {
            Result.Message = Error;
            return Result;
        }
        ++Result.TransitionCount;
    }

    Result.bSuccess = true;
    Result.Message = FString::Printf(
        TEXT("Imported %d supports and %d transitions"),
        Result.SupportCount,
        Result.TransitionCount);
    return Result;
}
