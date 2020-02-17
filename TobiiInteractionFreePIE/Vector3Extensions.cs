using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tobii.Interaction;

namespace TobiiInteractionFreePIE {
    public class SubtractableVector3 {
        public Vector3 Value { get; }

        public SubtractableVector3(Vector3 v) {
            this.Value = v;
        }

        public static Vector3 operator -(SubtractableVector3 v, Vector3 v2) {
            Vector3 v1 = v.Value;
            return new Vector3(
                v1.X - v2.X,
                v1.Y - v2.Y,
                v1.Z - v2.Z
            );
        }

        public static Vector3 operator +(SubtractableVector3 v, Vector3 v2) {
            Vector3 v1 = v.Value;
            return new Vector3(
                v1.X + v2.X,
                v1.Y + v2.Y,
                v1.Z + v2.Z
            );
        }

        public static Vector3 operator *(SubtractableVector3 v, float value) {
            Vector3 v1 = v.Value;
            return new Vector3(
                v1.X * value,
                v1.Y * value,
                v1.Z * value
            );
        }

        public static Vector3 operator /(SubtractableVector3 v, float value) {
            Vector3 v1 = v.Value;
            return new Vector3(
                v1.X / value,
                v1.Y / value,
                v1.Z / value
            );
        }

        public static implicit operator Vector3(SubtractableVector3 v) {
            return v.Value;
        }
        public static implicit operator SubtractableVector3(Vector3 v) {
            return new SubtractableVector3(v);
        }
    }
}
