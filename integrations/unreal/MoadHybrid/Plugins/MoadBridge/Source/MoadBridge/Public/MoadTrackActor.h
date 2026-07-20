#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "MoadTrackActor.generated.h"

class USplineComponent;

UCLASS()
class MOADBRIDGE_API AMoadTrackActor : public AActor
{
    GENERATED_BODY()

public:
    AMoadTrackActor();

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "MoAD")
    TObjectPtr<USplineComponent> Spline;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "MoAD")
    FString ContractId;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "MoAD")
    FString LaneId;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "MoAD")
    FString TargetLaneId;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "MoAD")
    FString TraversalType;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "MoAD")
    float AuthoredScale = 1.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "MoAD")
    bool bIsTransition = false;
};
