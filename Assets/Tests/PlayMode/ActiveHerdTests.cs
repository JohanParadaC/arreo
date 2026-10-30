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

        [UnityTest]
        public IEnumerator RemovedCow_IsHiddenKeepsItsIndex_AndTheRestKeepHerding()
        {
            GameObject ground = CreateGround();
            var settings = ScriptableObject.CreateInstance<HerdSettings>();
            ActiveHerd herd = CreateHerd(settings, count: 8, out GameObject cowTemplate);
            yield return null;
            ActivateCows(herd);
            yield return new WaitForFixedUpdate();

            herd.Remove(3);
            herd.Remove(3);
            Vector3 left = herd.Positions[3];
            float startSpread = herd.Positions.Where((_, i) => i != 3).Average(p => p.magnitude);
            herd.TriggerStampede(Vector3.zero);
            yield return new WaitForSeconds(1f);

            Assert.That(herd.Count, Is.EqualTo(8), "a removed cow keeps its index");
            Assert.That(herd.PresentCount, Is.EqualTo(7), "removing the same cow twice counts once");
            Assert.That(herd.Present[3], Is.False);
            Assert.That(herd.Cows[3].gameObject.activeSelf, Is.False, "the removed cow is still visible");
            Assert.That(herd.Positions[3], Is.EqualTo(left), "the removed cow still moves");
            Assert.That(herd.Positions.Where((_, i) => i != 3).Average(p => p.magnitude), Is.GreaterThan(startSpread + 4f),
                "the cows still in the herd stopped moving");

            Object.Destroy(herd.gameObject);
            Object.Destroy(cowTemplate);
            Object.Destroy(ground);
            Object.Destroy(settings);
        }

        [UnityTest]
        public IEnumerator Restock_BringsEveryCowBackWhereTheHerdStarted()
        {
            GameObject ground = CreateGround();
            var settings = ScriptableObject.CreateInstance<HerdSettings>();
            ActiveHerd herd = CreateHerd(settings, count: 8, out GameObject cowTemplate);
            yield return null;
            ActivateCows(herd);
            // Read once the cows are active: the herd spawned them from an inactive template.
            yield return new WaitForFixedUpdate();
            Vector3[] start = herd.Positions.ToArray();

            herd.TriggerStampede(Vector3.zero);
            yield return new WaitForSeconds(1f);
            herd.Remove(0);
            herd.Remove(5);
            herd.Restock();

            Assert.That(herd.PresentCount, Is.EqualTo(8));
            Assert.That(herd.Cows.All(cow => cow.gameObject.activeSelf), Is.True, "a sold cow did not come back");
            for (int i = 0; i < start.Length; i++)
                Assert.That(Vector3.Distance(herd.Positions[i], start[i]), Is.LessThan(0.05f), $"cow {i} is not back at its start");

            Object.Destroy(herd.gameObject);
            Object.Destroy(cowTemplate);
            Object.Destroy(ground);
            Object.Destroy(settings);
        }

        private static GameObject CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(300f, 1f, 300f);
            return ground;
        }

        // Cows come from an inactive template, like the other tests: the herd spawns them in Start.
        private static ActiveHerd CreateHerd(HerdSettings settings, int count, out GameObject cowTemplate)
        {
            cowTemplate = new GameObject("CowTemplate");
            var cowBody = cowTemplate.AddComponent<Rigidbody>();
            cowBody.constraints = RigidbodyConstraints.FreezeRotation;
            var cowCollider = cowTemplate.AddComponent<CapsuleCollider>();
            cowCollider.center = new Vector3(0f, 0.55f, 0f);
            cowCollider.radius = 0.55f;
            cowCollider.height = 2.1f;
            cowCollider.direction = 2;
            cowCollider.sharedMaterial = new PhysicsMaterial { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            cowTemplate.SetActive(false);

            var herdObject = new GameObject("Herd");
            herdObject.SetActive(false);
            var herd = herdObject.AddComponent<ActiveHerd>();
            herd.Configure(settings, cowBody, rider: null, count);
            herdObject.SetActive(true);
            return herd;
        }

        private static void ActivateCows(ActiveHerd herd)
        {
            foreach (Rigidbody cow in herd.Cows)
                cow.gameObject.SetActive(true);
        }
    }
}
