using UnityEngine;

namespace Arreo.Rope
{
    /// <summary>Marks something the lasso can catch, and where its head is.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LassoTarget : MonoBehaviour
    {
        [SerializeField] private Transform _head;

        private Rigidbody _body;

        public Rigidbody Body => _body != null ? _body : _body = GetComponent<Rigidbody>();
        public Vector3 HeadPosition => _head != null ? _head.position : transform.position;

        public void SetHead(Transform head) => _head = head;
    }
}
