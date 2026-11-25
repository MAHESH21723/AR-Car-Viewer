using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarViewer
{
    /// <summary>One paint option: a display name, a swatch colour and the body texture.</summary>
    [Serializable]
    public class CarPaint
    {
        public string name;
        public Color swatch = Color.white;
        public Texture texture;
    }

    /// <summary>
    /// Wraps the car model: fits it to a real-world size, paints it, animates the
    /// explode view and handles scale / rotation. Lives on the "car root" object that
    /// the placement controller moves onto detected planes.
    /// </summary>
    public class CarModel : MonoBehaviour
    {
        public const float DefaultLengthMeters = 1.2f;
        public const float MinScaleFactor = 0.1f;
        public const float MaxScaleFactor = 1.5f;
        const float ExplodeDuration = 0.6f;
        const float AutoRotateSpeed = 25f;

        class Part
        {
            public Transform t;
            public Vector3 startLocal;
            public Vector3 dirInRoot;   // direction in car-root space (unit scale)
            public float distance;      // in car-root units at scale 1
        }

        readonly List<Part> parts = new List<Part>();
        readonly List<Renderer> renderers = new List<Renderer>();
        Transform model;
        Material originalMaterial, paintMaterial;
        float nativeLength = 1f;
        float baseScale = 1f;
        float scaleFactor = 1f;
        float explode, explodeTarget;
        float popT = 1f;

        [Tooltip("Optional soft blob shadow drawn under the car.")]
        public Material shadowMaterial;

        public bool AutoRotate { get; set; }
        public bool IsExploded => explodeTarget > 0.5f;
        public int PaintIndex { get; private set; }
        public float ScaleFactor
        {
            get => scaleFactor;
            set { scaleFactor = Mathf.Clamp(value, MinScaleFactor, MaxScaleFactor); ApplyScale(); }
        }

        /// <summary>Instantiates the prefab as a child, centres it and records the explode parts.</summary>
        public void Build(GameObject prefab, CarPaint[] paints)
        {
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            model = Instantiate(prefab, transform).transform;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.name = "CarModel";

            model.GetComponentsInChildren(true, renderers);
            var bounds = GetBounds();
            nativeLength = Mathf.Max(bounds.size.x, bounds.size.z);
            if (nativeLength < 0.0001f) nativeLength = 1f;

            // Put the pivot at the bottom centre of the car so it sits on the plane.
            model.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            bounds = GetBounds();
            baseScale = DefaultLengthMeters / nativeLength;
            Vector3 centre = transform.InverseTransformPoint(bounds.center);

            // Explode parts. Offsets are relative to the car, so they work at any scale.
            AddPart("Body", centre, Vector3.up, 0.45f);
            AddPart("Spoiler", centre, Vector3.up * 0.45f, 0.35f, true);
            foreach (var r in renderers)
            {
                if (r.name.ToLowerInvariant().Contains("wheel"))
                    AddPart(r.transform, centre, Vector3.zero, 0.32f, true);
            }

            // One shared paint material for every part that uses the base colour material.
            if (renderers.Count > 0 && renderers[0].sharedMaterials.Length > 0)
            {
                foreach (var r in renderers)
                {
                    if (r.sharedMaterials.Length > 0 && originalMaterial == null)
                        originalMaterial = r.sharedMaterials[0];
                }
                if (originalMaterial != null)
                {
                    paintMaterial = new Material(originalMaterial);
                    foreach (var r in renderers)
                    {
                        var mats = r.sharedMaterials;
                        if (mats.Length > 0 && mats[0] == originalMaterial)
                        {
                            mats[0] = paintMaterial;
                            r.sharedMaterials = mats;
                        }
                    }
                }
            }

            CreateShadow();
            scaleFactor = 1f;
            ApplyScale();
        }

        void CreateShadow()
        {
            if (shadowMaterial == null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "BlobShadow";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, 0.003f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * (nativeLength * 1.6f);
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = shadowMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        Bounds GetBounds()
        {
            var b = new Bounds();
            bool first = true;
            foreach (var r in renderers)
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        void AddPart(string partName, Vector3 centre, Vector3 extraDir, float dist, bool horizontalFromCentre = false)
        {
            foreach (var r in renderers)
            {
                if (r.name.Equals(partName, StringComparison.OrdinalIgnoreCase))
                {
                    AddPart(r.transform, centre, extraDir, dist, horizontalFromCentre);
                    return;
                }
            }
        }

        void AddPart(Transform t, Vector3 centre, Vector3 extraDir, float dist, bool horizontalFromCentre)
        {
            var rend = t.GetComponent<Renderer>();
            Vector3 c = rend != null ? transform.InverseTransformPoint(rend.bounds.center) : t.position;
            Vector3 dir = extraDir;
            if (horizontalFromCentre)
            {
                Vector3 flat = c - centre;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f) dir += flat.normalized;
            }
            parts.Add(new Part
            {
                t = t,
                startLocal = t.localPosition,
                dirInRoot = dir.normalized,
                distance = dist * nativeLength
            });
        }

        /// <summary>Places the car on a plane point, facing the given world position.</summary>
        public void PlaceAt(Vector3 position, Vector3 faceTowards)
        {
            Vector3 look = faceTowards - position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f) look = Vector3.forward;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(look.normalized, Vector3.up));
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            popT = 0f;
            ApplyScale();
        }

        public void MoveTo(Vector3 position)
        {
            transform.position = position;
        }

        public void Hide() => gameObject.SetActive(false);

        public void ResetAll()
        {
            AutoRotate = false;
            explode = explodeTarget = 0f;
            scaleFactor = 1f;
            ApplyExplode();
            ApplyScale();
            Hide();
        }

        public void RotateBy(float degrees) => transform.Rotate(Vector3.up, degrees, Space.World);

        public void SetExploded(bool value) => explodeTarget = value ? 1f : 0f;

        public void ApplyPaint(CarPaint[] paints, int index)
        {
            if (paintMaterial == null || paints == null || index < 0 || index >= paints.Length) return;
            PaintIndex = index;
            if (paints[index].texture != null) paintMaterial.mainTexture = paints[index].texture;
        }

        void ApplyScale()
        {
            float pop = EaseOutBack(Mathf.Clamp01(popT));
            transform.localScale = Vector3.one * (baseScale * scaleFactor * Mathf.Max(0.001f, pop));
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        void Update()
        {
            if (popT < 1f)
            {
                popT += Time.deltaTime / 0.4f;
                ApplyScale();
            }
            if (AutoRotate) transform.Rotate(Vector3.up, AutoRotateSpeed * Time.deltaTime, Space.World);
            if (!Mathf.Approximately(explode, explodeTarget))
            {
                explode = Mathf.MoveTowards(explode, explodeTarget, Time.deltaTime / ExplodeDuration);
                ApplyExplode();
            }
        }

        void ApplyExplode()
        {
            float e = Mathf.SmoothStep(0f, 1f, explode);
            foreach (var p in parts)
            {
                if (p.t == null || p.t.parent == null) continue;
                Vector3 world = transform.TransformVector(p.dirInRoot * (p.distance * e));
                p.t.localPosition = p.startLocal + p.t.parent.InverseTransformVector(world);
            }
        }
    }
}

