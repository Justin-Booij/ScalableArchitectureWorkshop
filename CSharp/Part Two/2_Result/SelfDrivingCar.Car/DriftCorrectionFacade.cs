using SelfDrivingCar.Car.InternalTools;
using SelfDrivingCar.World;

namespace SelfDrivingCar.Car;

public class DriftCorrectionFacade
{
	private readonly InertialMeasurementUnit imu;
	private readonly SignReader signReader;

	public DriftCorrectionFacade(
		InertialMeasurementUnit imu,
		SignReader signReader)
	{
		this.imu = imu;
		this.signReader = signReader;
	}

	public (double SpeedCorrection, double BearingCorrection) GetCorrections(
		Road road,
		double currentSpeed,
		double currentBearing)
	{
		var speedLimit = signReader.GetSpeedForCurrentRoad(road);
		var targetBearing = imu.GetTargetHeading(road);

		return (speedLimit - currentSpeed, targetBearing - currentBearing);
	}
}