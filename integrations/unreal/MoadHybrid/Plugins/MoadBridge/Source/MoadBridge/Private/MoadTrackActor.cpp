#include "MoadTrackActor.h"

#include "Components/SplineComponent.h"

AMoadTrackActor::AMoadTrackActor()
{
    Spline = CreateDefaultSubobject<USplineComponent>(TEXT("TrackSpline"));
    SetRootComponent(Spline);
    Spline->SetClosedLoop(false);
    Spline->bDrawDebug = true;
    Tags.Add(TEXT("MoadGenerated"));
}
