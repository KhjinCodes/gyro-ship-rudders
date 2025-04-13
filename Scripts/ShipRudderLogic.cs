using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace Khjin.ShipRudder
{
    public class ShipRudderLogic : ModManager
    {
        private IMyCubeGrid grid;
        public bool IsMarkedForClose { get; set; }

        // Mod Settings
        private ShipRudderSettings settings = null;

        // Control blocks
        private List<IMyGyro> gyros = null;
        private List<IMyShipController> shipControllers = null;
        private List<IMyGyro> tempGyros = null;
        private List<IMyShipController> tempControllers = null;
        private IEnumerable<IMyCubeBlock> fatBlocks;
        private bool isSetup = false;

        // Ship angle 
        private double angleRoll = 0;
        private double anglePitch = 0;
        private double angleRollAdj = 0;
        private double anglePitchAdj = 0;
        private bool isAligning = false;

        // Correction and steering modifiers
        private const double standardSpeed = 30; // m/s
        private double yawSpeedModifier = 0;

        // PID
        private const double proportionalConstant = 2.0;
        private const double derivativeConstant = 5.0;
        private const int MAX_SETUP_TICKS = 300;
        private int setupTicks = MAX_SETUP_TICKS;

        // Soft Correction
        private const int MAX_IDLE_CORRECTION_TICKS = 90;
        private const double IDLE_CORRECTION_PITCH = 5;
        private const double IDLE_CORRECTION_ROLL = 3;
        private int idleCorrectionTicks = 0;
        private bool idleCorrect = false;

        private PIDController pitchPID;
        private PIDController rollPID;

        public ShipRudderLogic(IMyCubeGrid grid)
        {
            this.grid = grid;

            gyros = new List<IMyGyro>();
            shipControllers = new List<IMyShipController>();

            tempGyros = new List<IMyGyro>();
            tempControllers = new List<IMyShipController>();

            setupTicks = new Random((int)grid.EntityId).Next(60, 180);

            pitchPID = new PIDController(proportionalConstant, 0, derivativeConstant);
            rollPID = new PIDController(proportionalConstant, 0, derivativeConstant);
        }

        public override void LoadData()
        {
            settings = ShipRudderSession.Instance.Settings;
            angleRollAdj = settings.maxrollangle;
            anglePitchAdj = settings.maxpitchangle;
        }

        public override void UnloadData()
        {
            settings = null;

            grid = null;
            settings = null;
            pitchPID = null;
            rollPID = null;

            gyros.Clear();
            shipControllers.Clear();
        }

        public void Update()
        {
            // Get blocks
            if (!isSetup || setupTicks >= MAX_SETUP_TICKS)
            {
                isSetup = Setup();
                setupTicks = 0;
            }

            if (!isSetup || grid.IsStatic)
            {
                return;
            }

            // Get reference controller
            IMyShipController referenceBlock = GetControlledShipController(shipControllers);

            // Get gravity vector
            var gravityVec = referenceBlock.GetNaturalGravity();
            var gravityVecLength = gravityVec.Length();

            // If there's no gravity, don't update
            if (gravityVec.LengthSquared() == 0)
            {
                SetGyroOverride(false);
                anglePitch = 0;
                angleRoll = 0;
                return;
            }

            // Direction and vectors of the reference block
            var referenceForward = referenceBlock.WorldMatrix.Forward;
            var referenceLeft = referenceBlock.WorldMatrix.Left;

            // Get roll and pitch angles
            anglePitch = Math.Acos(MathHelper.Clamp(gravityVec.Dot(referenceForward) / gravityVecLength, -1, 1)) - Math.PI / 2;
            Vector3D planetRelativeLeftVec = referenceForward.Cross(gravityVec);
            angleRoll = VectorAngleBetween(referenceLeft, planetRelativeLeftVec);
            angleRoll *= VectorCompareDirection(VectorProjection(referenceLeft, gravityVec), gravityVec); //ccw is positive
            // anglePitch *= -1; angleRoll *= -1;

            // Get input indicator
            var inputVec = referenceBlock.MoveIndicator;

            // Check if a correction has to be done
            double anglePitchDeg = Math.Abs(MathHelper.ToDegrees(anglePitch));
            double angleRollDeg = Math.Abs(MathHelper.ToDegrees(angleRoll));

            // Soft correction so even if not turning, correction will be applied
            if (!isAligning && anglePitchDeg > IDLE_CORRECTION_PITCH)
            {
                idleCorrect = true;
                anglePitchAdj = (anglePitchDeg > anglePitchAdj ? anglePitchDeg : anglePitchAdj);
            }
            if (!isAligning && angleRollDeg > IDLE_CORRECTION_ROLL)
            {
                idleCorrect = true;
                angleRollAdj = (angleRollDeg > angleRollAdj ? angleRollDeg : angleRollAdj);
            }
            if(idleCorrect && idleCorrectionTicks == MAX_IDLE_CORRECTION_TICKS)
            {
                isAligning = true;
                idleCorrect = false;
                idleCorrectionTicks = 0;
            }

            // Absolute correction, no timer needed, will correct right away
            if (anglePitchDeg > settings.maxpitchangle)
            {
                isAligning = true;
                anglePitchAdj = (anglePitchDeg > anglePitchAdj ? anglePitchDeg : anglePitchAdj);
            }
            if (angleRollDeg > settings.maxrollangle)
            {
                isAligning = true;
                angleRollAdj = (angleRollDeg > angleRollAdj ? angleRollDeg : angleRollAdj);
            }

            if (anglePitchDeg <= 0.8 && angleRollDeg <= 0.8)
            {
                isAligning = false;
                idleCorrect = false;
                idleCorrectionTicks = 0;
                anglePitchAdj = settings.maxpitchangle;
                angleRollAdj = settings.maxrollangle;
            }

            if (isAligning || inputVec.X != 0)
            {
                // Angle controller
                double rollSpeed = rollPID.Update(angleRoll);
                double pitchSpeed = pitchPID.Update(anglePitch);
                double yawSpeed = 0;

                pitchSpeed *= MathHelper.Clamp(1.0 - (anglePitchDeg / anglePitchAdj), 0.01, settings.correctionspeed);
                rollSpeed *= MathHelper.Clamp(1.0 - (angleRollDeg / angleRollAdj), 0.01, settings.correctionspeed);

                // Adjust yaw speed based on ship speed
                // double shipSpeed = referenceBlock.GetShipSpeed();
                Vector3D shipVelocity = referenceBlock.GetShipVelocities().LinearVelocity;
                double dotProduct = Vector3D.Dot(shipVelocity, referenceForward);
                double shipSpeed = Math.Abs(dotProduct);

                if (inputVec.X != 0 && shipSpeed >= settings.minspeedtoturnmps)
                {
                    yawSpeedModifier = MathHelper.Clamp((shipSpeed / standardSpeed),
                        settings.minturnspeedmodifier,
                        settings.maxturnspeedmodifier);
                    yawSpeed = inputVec.X * settings.maxturnspeed * yawSpeedModifier;
                }

                SetGyroPower(1.0f);
                ApplyGyroOverride(pitchSpeed, yawSpeed, -rollSpeed, gyros, referenceBlock);
            }
            else
            {
                SetGyroPower(0.001f);
                ApplyGyroOverride(0, 0, 0, gyros, referenceBlock);
            }
        }

        public void UpdateTicks()
        {
            if(setupTicks < MAX_SETUP_TICKS)
            {
                setupTicks++;
            }

            if(idleCorrect && idleCorrectionTicks < MAX_IDLE_CORRECTION_TICKS)
            {
                idleCorrectionTicks++;
            }
        }

        private bool Setup()
        {
            bool result = false;

            lock(gyros) { 
            lock(shipControllers) {

                tempGyros.Clear();
                tempControllers.Clear();
                fatBlocks = grid.GetFatBlocks<IMyCubeBlock>();

                try
                {
                    MyAPIGateway.Parallel.ForEach(fatBlocks, block =>
                    {
                        if (block is IMyGyro)
                        {
                            IMyGyro gyro = (IMyGyro)block;

                            if (gyro.BlockDefinition.SubtypeId.ToLower().Contains("shiprudder")
                            || settings.allowvanillagyros && gyro.CustomName.ToLower().Contains("rudder"))
                            {
                                tempGyros.Add(gyro);
                            }
                        }
                        else if (block is IMyShipController && (block as IMyShipController).CanControlShip)
                        {
                            tempControllers.Add((IMyShipController)block);
                        }
                    });
                                            
                    gyros = tempGyros;
                    shipControllers = tempControllers;
                }
                catch { /* DO NOTHING */ }

                if (shipControllers.Count > 0 && gyros.Count > 0)
                {
                    result = true;
                }
            }}

            return result;
        }

        private IMyShipController GetControlledShipController(List<IMyShipController> controllers)
        {
            IMyShipController foundController;
            if(controllers.Count > 0) { foundController = controllers[0]; }
            else { foundController = null; }

            MyAPIGateway.Parallel.ForEach(controllers, thisController =>
            {
                if(!thisController.IsUnderControl
                || !thisController.CanControlShip)
                { return; }
                else
                { foundController = thisController; }
            });

            return foundController;
        }

        private void SetGyroOverride(bool value)
        {
            MyAPIGateway.Parallel.ForEach(gyros, gyro => {
                if (gyro.GyroOverride != value)
                { lock (gyro) { gyro.GyroOverride = value; } }
            });
        }

        private void SetGyroPower(float value)
        {
            MyAPIGateway.Parallel.ForEach(gyros, gyro => {
                if (gyro.GyroPower != value)
                { lock (gyro) { gyro.GyroPower = value; } }
            });
        }

        public IMyCubeGrid CubeGrid
        {
            get { return grid; }
        }

        public int GyroCount
        {
            get { return gyros.Count; }
        }

        public class PIDController
        {
            private double Kp;     // Proportional gain
            private double Ki;     // Integral gain
            private double Kd;     // Derivative gain
            private double setpoint;
            private double integral;
            private double previousError;

            public PIDController(double kp, double ki, double kd)
            {
                Kp = kp;
                Ki = ki;
                Kd = kd;
                integral = 0;
                previousError = 0;
            }

            public double Update(double current)
            {
                double error = setpoint - current;

                // Proportional term
                double proportional = Kp * error;

                // Integral term
                integral += error;
                double integralTerm = Ki * integral;

                // Derivative term
                double derivative = Kd * (error - previousError);
                previousError = error;

                // Calculate the control output
                double output = proportional + integralTerm + derivative;

                return output;
            }

            public void SetPoint(double sp)
            {
                setpoint = sp;
                integral = 0;
                previousError = 0;
            }

            public void Reset()
            {
                integral = 0;
                previousError = 0;
            }
        }

        #region Whip's Classes and Functions
        Vector3D VectorProjection(Vector3D a, Vector3D b) //proj a on b
        {
            Vector3D projection = a.Dot(b) / b.LengthSquared() * b;
            return projection;
        }

        int VectorCompareDirection(Vector3D a, Vector3D b) //returns -1 if vectors return negative dot product
        {
            double check = a.Dot(b);
            if (check < 0)
                return -1;
            else
                return 1;
        }

        double VectorAngleBetween(Vector3D a, Vector3D b) //returns radians
        {
            if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
        }

        //Whip's ApplyGyroOverride Method v9 - 8/19/17
        void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference)
        {
            //because keen does some weird stuff with signs
            var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed);
            var shipMatrix = reference.WorldMatrix;
            var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix);

            MyAPIGateway.Parallel.ForEach(gyro_list, gyro => 
            {
                lock (gyro)
                {
                    var gyroMatrix = gyro.WorldMatrix;
                    var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec,
                                                 Matrix.Transpose(gyroMatrix));

                    gyro.Pitch = (float)transformedRotationVec.X;
                    gyro.Yaw = (float)transformedRotationVec.Y;
                    gyro.Roll = (float)transformedRotationVec.Z;
                    gyro.GyroOverride = true;
                }
            });
        }

        #endregion
    }
}
