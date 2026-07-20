#pragma once

#include "CoreMinimal.h"
#include "EditorSubsystem.h"
#include "MoadSceneImportSubsystem.generated.h"

USTRUCT(BlueprintType)
struct FMoadImportResult
{
    GENERATED_BODY()

    UPROPERTY(BlueprintReadOnly, Category = "MoAD")
    bool bSuccess = false;

    UPROPERTY(BlueprintReadOnly, Category = "MoAD")
    int32 SupportCount = 0;

    UPROPERTY(BlueprintReadOnly, Category = "MoAD")
    int32 TransitionCount = 0;

    UPROPERTY(BlueprintReadOnly, Category = "MoAD")
    FString Message;
};

UCLASS()
class MOADBRIDGE_API UMoadSceneImportSubsystem : public UEditorSubsystem
{
    GENERATED_BODY()

public:
    UFUNCTION(BlueprintCallable, Category = "MoAD")
    FMoadImportResult ImportSceneContract(const FString& AbsoluteSceneJsonPath, bool bReplaceGeneratedActors = true);
};
