using System.Collections.Generic;
using UnityEngine;

namespace Arreo.Player
{
    /// <summary>
    /// Throwaway ragdoll of the rider, built from primitives when they're thrown off: torso, head,
    /// arms and legs on character joints. Masses stay within ~10x of each other so the joints hold.
    /// </summary>
    public sealed class RiderRagdoll : MonoBehaviour
    {
        private readonly List<Rigidbody> _parts = new List<Rigidbody>();

        public Rigidbody Torso { get; private set; }

        public static RiderRagdoll Spawn(Vector3 torsoPosition, Quaternion rotation, Vector3 velocity, float spin,
            Material clothes, Material skin, IEnumerable<Collider> ignore)
        {
            var root = new GameObject("RiderRagdoll").AddComponent<RiderRagdoll>();
            root.Build(torsoPosition, rotation, clothes, skin);

            foreach (Rigidbody part in root._parts)
            {
                part.linearVelocity = velocity;
                foreach (Collider other in ignore)
                    Physics.IgnoreCollision(part.GetComponent<Collider>(), other);
            }
            // Tumble head over heels around the axis across the throw.
            Vector3 across = Vector3.Cross(Vector3.up, new Vector3(velocity.x, 0f, velocity.z)).normalized;
            root.Torso.angularVelocity = across * spin;
            return root;
        }

        private void Build(Vector3 torsoPosition, Quaternion rotation, Material clothes, Material skin)
        {
            transform.SetPositionAndRotation(torsoPosition, rotation);
            // Anchors are in each part's own (unscaled) space: the top of a limb, the bottom of the head.
            Vector3 top = new Vector3(0f, 1f, 0f);
            Torso = Part(PrimitiveType.Capsule, "Torso", Vector3.zero, new Vector3(0.45f, 0.4f, 0.3f), 40f, clothes, null, Vector3.zero);
            Part(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.6f, 0f), Vector3.one * 0.28f, 6f, skin, Torso, new Vector3(0f, -0.5f, 0f));
            Part(PrimitiveType.Capsule, "ArmL", new Vector3(-0.35f, 0.15f, 0f), new Vector3(0.12f, 0.3f, 0.12f), 4f, clothes, Torso, top);
            Part(PrimitiveType.Capsule, "ArmR", new Vector3(0.35f, 0.15f, 0f), new Vector3(0.12f, 0.3f, 0.12f), 4f, clothes, Torso, top);
            Part(PrimitiveType.Capsule, "LegL", new Vector3(-0.13f, -0.75f, 0f), new Vector3(0.16f, 0.4f, 0.16f), 9f, clothes, Torso, top);
            Part(PrimitiveType.Capsule, "LegR", new Vector3(0.13f, -0.75f, 0f), new Vector3(0.16f, 0.4f, 0.16f), 9f, clothes, Torso, top);
        }

        private Rigidbody Part(PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, float mass,
            Material material, Rigidbody parent, Vector3 anchor)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;

            var body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.angularDamping = 0.5f;
            body.solverIterations = 12;

            if (parent != null)
            {
                var joint = go.AddComponent<CharacterJoint>();
                joint.connectedBody = parent;
                joint.anchor = anchor;
                joint.enableProjection = true;
                joint.lowTwistLimit = new SoftJointLimit { limit = -40f };
                joint.highTwistLimit = new SoftJointLimit { limit = 40f };
                joint.swing1Limit = new SoftJointLimit { limit = 60f };
                joint.swing2Limit = new SoftJointLimit { limit = 40f };
            }

            _parts.Add(body);
            return body;
        }
    }
}
