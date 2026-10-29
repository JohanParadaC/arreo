using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arreo.Horse;
using Arreo.Rope;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arreo.Tests.PlayMode
{
    /// <summary>
    /// Mass test for the verlet rope: a ~400 kg cow against a ~500 kg horse with rider.
    /// At rest nothing vibrates; pulling gives a steady tension with no whiplash.
    /// </summary>
    public class RopeMassTests
    {
        private const float CowMass = 400f;
        private const float HorseMass = 500f;

        private GameObject _ground;
        private HorseController _horse;
        private Rigidbody _horseBody;
        private Rigidbody _cow;
        private LassoTarget _target;
        private RopeSimulation _rope;
        private RopeSettings _ropeSettings;
        private HorseSettings _horseSettings;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);
            _ground.transform.localScale = new Vector3(400f, 1f, 400f);
            var frictionless = new PhysicsMaterial { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };

            var horse = new GameObject("Horse");
            _horseBody = horse.AddComponent<Rigidbody>();
            _horseBody.mass = HorseMass;
            _horseBody.constraints = RigidbodyConstraints.FreezeRotation;
            AddCapsule(horse, 0.7f, 2.6f, frictionless);
            _horseSettings = ScriptableObject.CreateInstance<HorseSettings>();
            _horse = horse.AddComponent<HorseController>();
            _horse.Configure(_horseSettings);

            var cow = new GameObject("Cow");
            cow.transform.position = new Vector3(0f, 0f, 9f);
            _cow = cow.AddComponent<Rigidbody>();
            _cow.mass = CowMass;
            _cow.constraints = RigidbodyConstraints.FreezeRotation;
            AddCapsule(cow, 0.55f, 2.1f, frictionless);
            var head = new GameObject("Head").transform;
            head.SetParent(cow.transform, false);
            head.localPosition = new Vector3(0f, 1.1f, 1.1f);
            _target = cow.AddComponent<LassoTarget>();
            _target.SetHead(head);

            _ropeSettings = ScriptableObject.CreateInstance<RopeSettings>();
            _rope = new GameObject("Rope").AddComponent<RopeSimulation>();
            _rope.Configure(_ropeSettings, _horseBody);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_rope.gameObject);
            Object.Destroy(_horse.gameObject);
            Object.Destroy(_cow.gameObject);
            Object.Destroy(_ground);
            Object.Destroy(_ropeSettings);
            Object.Destroy(_horseSettings);
        }

        [UnityTest]
        public IEnumerator SlackRopeAtRest_NothingVibrates()
        {
            _rope.Attach(_target);
            // The rope is laid out straight, so first it drops and swings; give that time to die out
            // (same 5 s as the EditMode rest test) before checking for vibration.
            yield return new WaitForSeconds(5f);

            float maxPointSpeed = 0f;
            float maxBodySpeed = 0f;
            Vector3[] previous = _rope.Points.ToArray();
            for (float t = 0f; t < 1f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                maxPointSpeed = Mathf.Max(maxPointSpeed, MaxPointSpeed(previous));
                maxBodySpeed = Mathf.Max(maxBodySpeed, _cow.linearVelocity.magnitude, _horseBody.linearVelocity.magnitude);
                previous = _rope.Points.ToArray();
            }

            Debug.Log($"[RopeMassTests] rest: max point speed {maxPointSpeed:0.0000} m/s, max body speed {maxBodySpeed:0.0000} m/s, state {_rope.State}");
            Assert.That(_rope.State, Is.EqualTo(RopeState.Lassoed));
            Assert.That(maxPointSpeed, Is.LessThan(0.01f), "rope points vibrate at rest");
            Assert.That(maxBodySpeed, Is.LessThan(0.01f), "cow or horse vibrate at rest");
        }

        [UnityTest]
        public IEnumerator FleeingCow_SteadyTensionNoWhiplash_DragsTheHorse()
        {
            _rope.Attach(_target);
            var tensions = new List<float>();
            float maxPointSpeed = 0f;
            float maxStretch = 0f;
            Vector3[] previous = _rope.Points.ToArray();

            for (float t = 0f; t < 4f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                FleeLikeTheHerd(new Vector3(0f, 0f, 6f));
                _horse.SetExternalVelocity(_rope.DragVelocity);

                AssertFinite();
                maxPointSpeed = Mathf.Max(maxPointSpeed, MaxPointSpeed(previous));
                previous = _rope.Points.ToArray();
                maxStretch = Mathf.Max(maxStretch, Vector3.Distance(_rope.SaddlePoint, _target.HeadPosition) / _rope.RestLength - 1f);
                if (t > 2f)
                    tensions.Add(_rope.Tension);
            }

            float mean = tensions.Average();
            float spread = Mathf.Sqrt(tensions.Average(x => (x - mean) * (x - mean)));
            Debug.Log($"[RopeMassTests] flee: tension {mean:0} ± {spread:0} N, max point speed {maxPointSpeed:0.00} m/s, " +
                      $"max stretch {maxStretch * 100f:0.0} %, horse dragged to z={_horseBody.position.z:0.00}");

            Assert.That(_rope.State, Is.EqualTo(RopeState.Taut));
            // A cow accelerating at 5 m/s² pulls about 400 kg * 5 = 2000 N.
            Assert.That(mean, Is.InRange(1600f, 2400f));
            Assert.That(spread, Is.LessThan(mean * 0.15f), "tension jitters");
            Assert.That(maxPointSpeed, Is.LessThan(10f), "the rope whips");
            Assert.That(maxStretch, Is.LessThan(0.1f), "the rope overstretched");
            Assert.That(_horseBody.position.z, Is.GreaterThan(0.5f), "the horse was not dragged");
        }

        [UnityTest]
        public IEnumerator GallopingAwayFromAResistingCow_SnapsTheRope()
        {
            _rope.Attach(_target);
            _horse.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _horse.Configure(_horseSettings);

            float peakTension = 0f;
            for (float t = 0f; t < 6f && _rope.State != RopeState.Broken; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                _horse.SetIntent(new HorseIntent(1f, 0f, gallop: true));
                FleeLikeTheHerd(Vector3.zero);
                AssertFinite();
                peakTension = Mathf.Max(peakTension, _rope.Tension);
            }

            Debug.Log($"[RopeMassTests] gallop away: peak tension {peakTension:0} N, state {_rope.State}");
            Assert.That(_rope.State, Is.EqualTo(RopeState.Broken));
        }

        [UnityTest]
        public IEnumerator ThrowAtAHead_CatchesIt()
        {
            Assert.That(_rope.Throw(_target.HeadPosition), Is.True);

            yield return new WaitForSeconds(1f);

            Assert.That(_rope.State, Is.EqualTo(RopeState.Lassoed).Or.EqualTo(RopeState.Taut));
            Assert.That(_rope.Caught, Is.EqualTo(_target));
        }

        [UnityTest]
        public IEnumerator ThrowAtEmptyGround_MissesAndCoilsBackInAboutASecond()
        {
            _rope.Throw(new Vector3(6f, 0f, 2f));
            yield return new WaitForSeconds(0.8f);
            Assert.That(_rope.State, Is.EqualTo(RopeState.Released));

            yield return new WaitForSeconds(1f);
            Assert.That(_rope.State, Is.EqualTo(RopeState.Coiled));
        }

        private void FleeLikeTheHerd(Vector3 desired)
        {
            Vector3 velocity = _cow.linearVelocity;
            Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), desired, 5f * Time.fixedDeltaTime);
            _cow.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }

        private float MaxPointSpeed(Vector3[] previous)
        {
            var points = _rope.Points;
            float max = 0f;
            for (int i = 1; i < points.Count - 1 && i < previous.Length; i++)
                max = Mathf.Max(max, Vector3.Distance(points[i], previous[i]) / Time.fixedDeltaTime);
            return max;
        }

        private void AssertFinite()
        {
            foreach (Vector3 p in _rope.Points)
                Assert.That(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z), Is.False, "NaN in the rope");
        }

        private static void AddCapsule(GameObject go, float radius, float height, PhysicsMaterial material)
        {
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = new Vector3(0f, radius, 0f);
            capsule.sharedMaterial = material;
        }
    }
}
