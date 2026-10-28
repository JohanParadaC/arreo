using System.Collections;
using Arreo.Horse;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arreo.Tests.PlayMode
{
    /// <summary>Smoke tests: the Rigidbody horse actually moves, gallops, turns and stops at walls.</summary>
    public class HorseControllerTests
    {
        private GameObject _ground;
        private HorseController _horse;
        private HorseSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);
            _ground.transform.localScale = new Vector3(400f, 1f, 400f);

            var go = new GameObject("Horse");
            var body = go.AddComponent<Rigidbody>();
            body.mass = 450f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            var collider = go.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 0.7f;
            collider.height = 2.6f;
            collider.center = new Vector3(0f, 0.7f, 0f);
            collider.sharedMaterial = new PhysicsMaterial { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };

            _settings = ScriptableObject.CreateInstance<HorseSettings>();
            _horse = go.AddComponent<HorseController>();
            _horse.Configure(_settings);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_horse.gameObject);
            Object.Destroy(_ground);
            Object.Destroy(_settings);
        }

        [UnityTest]
        public IEnumerator Gallop_MovesForwardAndDrainsStamina()
        {
            _horse.SetIntent(new HorseIntent(1f, 0f, true));

            yield return new WaitForSeconds(5f);

            Assert.That(_horse.Locomotion.IsGalloping, Is.True);
            Assert.That(_horse.transform.position.z, Is.GreaterThan(25f));
            Assert.That(Mathf.Abs(_horse.transform.position.x), Is.LessThan(0.5f));
            Assert.That(_horse.Locomotion.Stamina.Normalized, Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator Steer_RotatesTheHorse()
        {
            _horse.SetIntent(new HorseIntent(0f, 1f, false));

            yield return new WaitForSeconds(0.5f);

            Assert.That(_horse.transform.eulerAngles.y, Is.InRange(30f, 80f));
        }

        [UnityTest]
        public IEnumerator Wall_StopsTheHorse()
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 2f, 15f);
            wall.transform.localScale = new Vector3(20f, 4f, 1f);
            _horse.SetIntent(new HorseIntent(1f, 0f, true));

            yield return new WaitForSeconds(6f);

            Assert.That(_horse.transform.position.z, Is.LessThan(14f));
            Assert.That(_horse.Locomotion.Speed, Is.LessThan(2f));
            Object.Destroy(wall);
        }
    }
}
