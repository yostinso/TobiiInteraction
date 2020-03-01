using System;
using System.IO;
using System.Collections.Generic;
using FreePIE.Core.Contracts;
using Tobii.Interaction;
using Tobii.Interaction.Framework;

namespace TobiiInteractionFreePIE {
    [GlobalType(Type = typeof(TobiiInteractionGlobal))]
    public class TobiiInteractionPlugin : IPlugin {

        public event EventHandler Started;

        private Host host;
        private EyePositionStream eyeDataStream;
        private GazePointDataStream gazeDataStream;
        private Vector3 eyeOffsetMm;
        private Vector3 eyeOffsetNorm;
        private float targetRoll;
        private float targetYaw;
        private Vector3 targetAverageHeadPositionMm;
        private Vector3 targetAverageHeadPositionNorm;

        private EngineStateObserver<Size> displaySizeObserver;
        private EngineStateObserver<string> userProfileNameObserver;
        private EngineStateObserver<EyeTrackingDeviceStatus> eyeTrackingDeviceStatusObserver;
        private EngineStateObserver<UserPresence> userPresenceObserver;


        public float Yaw { get; private set; }
        public float Roll { get; private set; }
        // You can't calculate pitch using eye tracker at the moment.
        public Vector3 AverageHeadPositionMm { get; private set; }
        public Vector3 AverageHeadPositionNorm { get; private set; }

        public EyePositionData LastEyePositionData { get; private set; }
        public GazePointData LastGazePointData { get; private set; }

        public Vector2 NormalizedGazePoint { get; private set; }

        public Vector2 NormalizedCenterDelta { get; private set; }

        public UpdateReason UpdateReason { get; private set; }
        public UserPresence UserPresence { get; private set; }
        public EyeTrackingDeviceStatus DeviceStatus { get; private set; }
        public string CurrentProfileName { get; private set; }
        public Size DisplaySize { get; private set; }
        public TrackStatus TrackStatus { get; private set; }

        public object CreateGlobal() {
            return new TobiiInteractionGlobal(this);
        }

        public string FriendlyName {
            get { return "Tobii Interaction Framework"; }
        }

        public bool GetProperty(int index, IPluginProperty property) {
            return false;
        }

        public bool SetProperties(Dictionary<string, object> properties) {
            return true;
        }

        public Action Start() {
            host = new Host();
            var displaySizeTask = host.States.GetDisplaySizeAsync();
            displaySizeTask.Wait();
            DisplaySize = displaySizeTask.Result.Value;

            //host.UserPresenceChanged += HostOnUserPresenceChanged;
            userPresenceObserver = host.States.CreateUserPresenceObserver();
            userPresenceObserver.Changed += HostOnUserPresenceChanged;

            //host.EyeTrackingDeviceStatusChanged += HostOnEyeTrackingDeviceStatusChanged;
            eyeTrackingDeviceStatusObserver = host.States.CreateEyeTrackingDeviceStatusObserver();
            eyeTrackingDeviceStatusObserver.Changed += HostOnEyeTrackingDeviceStatusChanged;

            //host.UserProfileNameChanged += HostOnUserProfileNameChanged;
            userProfileNameObserver = host.States.CreateUserProfileNameObserver();
            userProfileNameObserver.Changed += HostOnUserProfileNameChanged;
            
            //host.DisplaySizeChanged += HostOnDisplaySizeChanged;
            displaySizeObserver = host.States.CreateDisplaySizeObserver();
            displaySizeObserver.Changed += HostOnDisplaySizeChanged;

            gazeDataStream = host.Streams.CreateGazePointDataStream(GazePointDataMode.Unfiltered);
            eyeDataStream = host.Streams.CreateEyePositionStream();

            gazeDataStream.Next += GazeDataStreamOnNext;
            eyeDataStream.Next += EyeDataStreamOnNext;

            if (Started != null)
            {
                Started(this, null);
            }

            return null;
        }

        public void Stop() {
            gazeDataStream.Next -= GazeDataStreamOnNext;
            eyeDataStream.Next -= EyeDataStreamOnNext;
            //gazeDataStream.Dispose();
            //eyeDataStream.Dispose();

            /*
            host.UserProfileNameChanged -= HostOnUserProfileNameChanged;
            host.EyeTrackingDeviceStatusChanged -= HostOnEyeTrackingDeviceStatusChanged;
            host.UserPresenceChanged -= HostOnUserPresenceChanged;
            */
            userPresenceObserver.Changed -= HostOnUserPresenceChanged;
            userPresenceObserver.Dispose();
            eyeTrackingDeviceStatusObserver.Changed -= HostOnEyeTrackingDeviceStatusChanged;
            eyeTrackingDeviceStatusObserver.Dispose();
            userProfileNameObserver.Changed -= HostOnUserProfileNameChanged;
            userProfileNameObserver.Dispose();
            displaySizeObserver.Changed -= HostOnDisplaySizeChanged;
            displaySizeObserver.Dispose();
            host.Dispose();
        }

        public void DoBeforeNextExecute() {
        }


        private void HostOnDisplaySizeChanged(object sender, EngineStateValue<Size> engineStateValue) {
            if (!engineStateValue.IsValid)
                return;

            DisplaySize = engineStateValue.Value;
            UpdateReason = UpdateReason.DisplaySizeChanged;
            //OnUpdate();
        }

        private void HostOnUserProfileNameChanged(object sender, EngineStateValue<string> engineStateValue) {
            if (!engineStateValue.IsValid)
                return;

            CurrentProfileName = engineStateValue.Value;
            UpdateReason = UpdateReason.CurrentProfileNameChanged;
            //OnUpdate();
        }

        private void HostOnEyeTrackingDeviceStatusChanged(object sender, EngineStateValue<EyeTrackingDeviceStatus> engineStateValue) {
            if (!engineStateValue.IsValid)
                return;

            DeviceStatus = engineStateValue.Value;
            UpdateReason = UpdateReason.DeviceStatusChanged;
            //OnUpdate();
        }

        private void HostOnUserPresenceChanged(object sender, EngineStateValue<UserPresence> engineStateValue) {
            if (!engineStateValue.IsValid)
                return;

            UserPresence = engineStateValue.Value;
            UpdateReason = UpdateReason.UserPresenceChanged;
            //OnUpdate();
        }

        private void EyeDataStreamOnNext(object sender, StreamData<EyePositionData> eyePositionStream) {
            EyePositionData data = eyePositionStream.Data;
            LastEyePositionData = data;
            UpdateReason = UpdateReason.EyeDataChanged;
            TrackStatus = TrackStatus.NoEyes;

            var rightEyePositionMm = new Vector3((float)data.RightEye.X, (float)data.RightEye.Y, (float)data.RightEye.Z);
            var leftEyePositionMm = new Vector3((float)data.LeftEye.X, (float)data.LeftEye.Y, (float)data.LeftEye.Z);
            var rightEyePositionNorm = new Vector3((float)data.RightEyeNormalized.X, (float)data.RightEyeNormalized.Y, (float)data.RightEyeNormalized.Z);
            var leftEyePositionNorm = new Vector3((float)data.LeftEyeNormalized.X, (float)data.LeftEyeNormalized.Y, (float)data.LeftEyeNormalized.Z);

            if (data.HasLeftEyePosition && data.HasRightEyePosition) {
                TrackStatus = TrackStatus.BothEyes;
                eyeOffsetMm = (SubtractableVector3)rightEyePositionMm - leftEyePositionMm;
                eyeOffsetNorm = (SubtractableVector3)rightEyePositionNorm - leftEyePositionNorm;
            } else if (data.HasLeftEyePosition) {
                TrackStatus = TrackStatus.OnlyLeftEye;
            } else if (data.HasRightEyePosition) {
                TrackStatus = TrackStatus.OnlyRightEye;
            }

            switch (TrackStatus) {
                case TrackStatus.BothEyes:
                    targetAverageHeadPositionMm = (SubtractableVector3)((SubtractableVector3)rightEyePositionMm + leftEyePositionMm) / 2f;
                    targetAverageHeadPositionNorm = (SubtractableVector3)((SubtractableVector3)rightEyePositionNorm + leftEyePositionNorm) / 2f;
                    break;

                case TrackStatus.OnlyLeftEye:
                    targetAverageHeadPositionMm = (SubtractableVector3)leftEyePositionMm + (SubtractableVector3)eyeOffsetMm / 2f;
                    targetAverageHeadPositionNorm = (SubtractableVector3)leftEyePositionNorm + (SubtractableVector3)eyeOffsetNorm / 2f;
                    break;

                case TrackStatus.OnlyRightEye:
                    targetAverageHeadPositionMm = (SubtractableVector3)rightEyePositionMm - (SubtractableVector3)eyeOffsetMm / 2f;
                    targetAverageHeadPositionNorm = (SubtractableVector3)rightEyePositionNorm - (SubtractableVector3)eyeOffsetNorm / 2f;
                    break;

                case TrackStatus.NoEyes:
                default:
                    //Don't update D:
                    break;
            }

            targetRoll = (float)Math.Atan2(eyeOffsetMm.Y, eyeOffsetMm.X);
            targetYaw = -(float)Math.Atan2(eyeOffsetMm.Z, eyeOffsetMm.X);

            Roll = Lerp(Roll, targetRoll, 0.6f);
            Yaw = Lerp(Yaw, targetYaw, 0.6f);

            AverageHeadPositionMm = Lerp(AverageHeadPositionMm, targetAverageHeadPositionMm, 0.6f);
            AverageHeadPositionNorm = Lerp(AverageHeadPositionNorm, targetAverageHeadPositionNorm, 0.6f);

            //OnUpdate();
        }

        private void GazeDataStreamOnNext(object sender, StreamData<GazePointData> gazePositionStream) {
            GazePointData data = gazePositionStream.Data;
            LastGazePointData = data;

            var gazePointX = data.X;
            var gazePointY = data.Y;


            var screenWidth = DisplaySize.Width;
            var screenHeight = DisplaySize.Height;


            var normalizedGazePointX = (float)Math.Min(Math.Max((gazePointX / screenWidth), 0.0), 1.0);
            var normalizedGazePointY = (float)Math.Min(Math.Max((gazePointY / screenHeight), 0.0), 1.0);

            NormalizedGazePoint = new Vector2(normalizedGazePointX, normalizedGazePointY);

            var normalizedDistanceFromCenterX = (normalizedGazePointX - 0.5f) * 2.0f;
            var normalizedDistanceFromCenterY = (normalizedGazePointY - 0.5f) * 2.0f;

            NormalizedCenterDelta = new Vector2(normalizedDistanceFromCenterX, normalizedDistanceFromCenterY);

            UpdateReason = UpdateReason.GazeDataChanged;
            //OnUpdate();
        }

        private static float Lerp(float lower, float higher, float alpha) {
            return lower + (higher - lower) * alpha;
        }

        private static Vector3 Lerp(Vector3 lower, Vector3 higher, float alpha) {
            
            return (SubtractableVector3)lower + (SubtractableVector3)((SubtractableVector3)higher - lower) * alpha;
        }
    }

    

#pragma warning disable IDE1006 // Naming Styles
    [Global(Name = "tobiiInteraction")]
    public class TobiiInteractionGlobal {
        private readonly TobiiInteractionPlugin plugin;
        public TobiiInteractionGlobal(TobiiInteractionPlugin plugin) {
            this.plugin = plugin;
        }

        public float yaw { get { return plugin.Yaw; } }
        public float roll { get { return plugin.Roll; } }

        public double normalizedCenterDeltaX { get { return plugin.NormalizedCenterDelta.X; } }
        public double normalizedCenterDeltaY { get { return plugin.NormalizedCenterDelta.Y; } }

        public double gazePointNormalizedX { get { return plugin.NormalizedGazePoint.X; } }
        public double gazePointNormalizedY { get { return plugin.NormalizedGazePoint.Y; } }

        public float gazePointInPixelsX { get { return (float)plugin.LastGazePointData.X; } }
        public float gazePointInPixelsY { get { return (float)plugin.LastGazePointData.Y; } }
        public float gazeDataTimestamp { get { return (float)plugin.LastGazePointData.Timestamp; } }

        public float leftEyePositionInMmX { get { return (float)plugin.LastEyePositionData.LeftEye.X; } }
        public float leftEyePositionInMmY { get { return (float)plugin.LastEyePositionData.LeftEye.Y; } }
        public float leftEyePositionInMmZ { get { return (float)plugin.LastEyePositionData.LeftEye.Z; } }

        public float rightEyePositionInMmX { get { return (float)plugin.LastEyePositionData.RightEye.X; } }
        public float rightEyePositionInMmY { get { return (float)plugin.LastEyePositionData.RightEye.Y; } }
        public float rightEyePositionInMmZ { get { return (float)plugin.LastEyePositionData.RightEye.Z; } }

        public float leftEyePositionNormalizedX { get { return (float)plugin.LastEyePositionData.LeftEyeNormalized.X; } }
        public float leftEyePositionNormalizedY { get { return (float)plugin.LastEyePositionData.LeftEyeNormalized.Y; } }
        public float leftEyePositionNormalizedZ { get { return (float)plugin.LastEyePositionData.LeftEyeNormalized.Z; } }

        public float rightEyePositionNormalizedX { get { return (float)plugin.LastEyePositionData.RightEyeNormalized.X; } }
        public float rightEyePositionNormalizedY { get { return (float)plugin.LastEyePositionData.RightEyeNormalized.Y; } }
        public float rightEyePositionNormalizedZ { get { return (float)plugin.LastEyePositionData.RightEyeNormalized.Z; } }

        public double averageEyePositionInMmX { get { return plugin.AverageHeadPositionMm.X; } }
        public double averageEyePositionInMmY { get { return plugin.AverageHeadPositionMm.Y; } }
        public double averageEyePositionInMmZ { get { return plugin.AverageHeadPositionMm.Z; } }

        public double averageEyePositionNormalizedX { get { return plugin.AverageHeadPositionNorm.X; } }
        public double averageEyePositionNormalizedY { get { return plugin.AverageHeadPositionNorm.Y; } }
        public double averageEyePositionNormalizedZ { get { return plugin.AverageHeadPositionNorm.Z; } }

        public float eyeDataTimestamp { get { return (float)plugin.LastEyePositionData.Timestamp; } }

        public string updateReason { get { return plugin.UpdateReason.ToString(); } }
        public string userPresence { get { return plugin.UserPresence.ToString(); } }
        public string deviceStatus { get { return plugin.DeviceStatus.ToString(); } }
        public string userProfileName { get { return plugin.CurrentProfileName; } }
        public double displaySizeX { get { return plugin.DisplaySize.Width; } }
        public double displaySizeY { get { return plugin.DisplaySize.Height; } }
    }
#pragma warning restore IDE1006 // Naming Styles

    public enum UpdateReason {
        GazeDataChanged,
        EyeDataChanged,
        UserPresenceChanged,
        DeviceStatusChanged,
        CurrentProfileNameChanged,
        DisplaySizeChanged
    }

    public enum TrackStatus {
        BothEyes,
        OnlyLeftEye,
        OnlyRightEye,
        NoEyes
    }
}
