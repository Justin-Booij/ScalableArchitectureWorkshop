using SelfDrivingCar.Application;
using SelfDrivingCar.Application.World;
using SelfDrivingCar.TomTom;

namespace SelfDrivingCar.Tests;

public class CarDriverTests {
  private List<Road> _testRoads = new List<Road>();
  private int TOTAL_DISTANCE_TEST_ROADS = 943;
  private int TOTAL_TIME_TEST_ROADS = 75456;

  [SetUp]
  public void Setup() {
    _testRoads.Add(new Road(){From = new Coordinate(1, 1), SpeedLimit = 1, To = new Coordinate(2, 2)});
    _testRoads.Add(new Road(){From = new Coordinate(2, 2), SpeedLimit = 1, To = new Coordinate(3, 3)});

  }

  [Test]
  public void CreatingCarDriver_WithRoadList_ShouldSetCurrentPositionAndCorrectBearing() {
    CarDriver carDriver = new CarDriver(_testRoads);
    double correctBearing = GeoMaths.CalculateBearing(_testRoads[0].From, _testRoads[0].To);

    Assert.That(_testRoads[0].From, Is.EqualTo(carDriver.CurrentPosition));
    Assert.That(correctBearing, Is.EqualTo(carDriver.CurrentBearing));
  }

  [Test]
  public void CreatingCarDriver_WithoutRoadList_ShouldNotHavePositionAndBearingZero() {
    CarDriver carDriver = new CarDriver(new List<Road>());
    Assert.That(carDriver.CurrentPosition, Is.Null);
    Assert.That(carDriver.CurrentBearing, Is.EqualTo(0));
  }

  [Test]
  public void GettingTotalDistanceRemaining_OnCarDriverWithRoad_ShouldGiveTotalDistance() {
    CarDriver carDriver = new CarDriver(_testRoads);

    Assert.That((int)carDriver.GetTotalDistanceRemaining(), Is.EqualTo(TOTAL_DISTANCE_TEST_ROADS));

  }

  [Test]
  public void GettingTotalTimeRemaining_OnCarDriverWithRoad_ShouldGiveTotalTimeRemaining() {
    CarDriver carDriver = new CarDriver(_testRoads);


    Assert.That((int)carDriver.GetTotalTimeRemaining(), Is.EqualTo(TOTAL_TIME_TEST_ROADS));
  }

}
