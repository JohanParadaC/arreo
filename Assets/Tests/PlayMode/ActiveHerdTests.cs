using System.Collections;
using System.Linq;
using Arreo.Herd;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arreo.Tests.PlayMode
{
    /// <summary>Smoke test: Rigidbody cows actually move away from a rider standing next to them.</summary>
    public class ActiveHerdTests
    {
        [UnityTest]
        public IEnumerator CowsMoveAwayFromNearbyRider()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);

            var cowTemplate = new GameObject("CowTemplate");
            var cowBody = cowTemplate.AddComponent<Rigidbody>();
            cowBody.constraints = RigidbodyConstraints.FreezeRotation;
            var cowCollider = cowTemplate.AddComponent<CapsuleCollider>();
            cowCollider.center = new Vector3(0f, 0.55f, 0f);
            cowCollider.radius = 0.55f;
            cowCollider.height = 2.1f;
            cowCollider.direction = 2;
            cowTemplate.SetActive(false);

            var rider = new GameObject("Rider").AddComponent<Rigidbody>();
            rider.isKinematic = true;
            rider.position = new Vector3(0f, 0f, -6f);

            var settings = ScriptableObject.CreateInstance<HerdSettings>();
            var herdObject = new GameObject("Herd");
            herdObject.SetActive(false);
            var herd = herdObject.AddComponent<ActiveHerd>();
            herd.Configure(settings, cowBody, rider, count: 10);
            herdObject.SetActive(true);

            yield return null;
            foreach (Transform cow in herdObject.transform)
                cow.gameObject.SetActive(true);
            float startDistance = herd.Positions.Average(p => Vector3.Distance(p, rider.position));

            yield return new WaitForSeconds(2f);

            float endDistance = herd.Positions.Average(p => Vector3.Distance(p, rider.position));
            Assert.That(endDistance, Is.GreaterThan(startDistance + 1f));

            Object.Destroy(herdObject);
            Object.Destroy(cowTemplate);
            Object.Destroy(rider.gameObject);
            Object.Destroy(ground);
            Object.Destroy(settings);
        }

        [UnityTest]
        public IEnumerator Stampede_ScattersTheWholeHerdAwayFromTheThunder()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(300f, 1f, 300f);

            var cowTemplate = new GameObject("CowTemplate");
            var cowBody = cowTemplate.AddComponent<Rigidbody>();
            cowBody.constraints = RigidbodyConstraints.FreezeRotation;
            var cowCollider = cowTemplate.AddComponent<CapsuleCollider>();
            cowCollider.center = new Vector3(0f, 0.55f, 0f);
            cowCollider.radius = 0.55f;
            cowCollider.height = 2.1f;
            cowCollider.direction = 2;
            cowCollider.sharedMaterial = new PhysicsMaterial { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            cowTemplate.SetActive(false);

            var settings = ScriptableObject.CreateInstance<HerdSettings>();
            var herdObject = new GameObject("Herd");
            herdObject.SetActive(false);
            var herd = herdObject.AddComponent<ActiveHerd>();
            herd.Configure(settings, cowBody, rider: null, count: 15);
            herdObject.SetActive(true);

            yield return null;
            foreach (Transform cow in herdObject.transform)
                cow.gameObject.SetActive(true);
            float startDistance = herd.Positions.Average(p => p.magnitude);

            herd.TriggerStampede(Vector3.zero);
            yield return new WaitForSeconds(2f);

            float endDistance = herd.Positions.Average(p => p.magnitude);
            Assert.That(herd.StampedeIntensity, Is.EqualTo(1f));
            Assert.That(endDistance, Is.GreaterThan(startDistance + 6f), "the herd did not scatter");

            Object.Destroy(herdObject);
            Object.Destroy(cowTemplate);
            Object.Destroy(ground);
            Object.Destroy(settings);
        }
    }
}
