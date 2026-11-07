using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// A small floating text panel for the palm-swing sandbox (ADR-0078): the drill's next bar, the count-in, the
    /// settings in force and the run summary. It is an experimenter's panel, not a learner interface — the learner's
    /// own display is <see cref="PalmSwingView"/>. Follows the head's heading the way the other panels do.
    /// </summary>
    public sealed class SandboxHud : MonoBehaviour
    {
        [SerializeField] private Transform _head;
        [SerializeField] private float _distanceMetres = 1.1f;
        [SerializeField] private float _heightOffsetMetres = 0.18f;
        [SerializeField] private float _followEaseSeconds = 0.35f;
        [SerializeField] private float _maxTrailMetres = 0.4f;
        [SerializeField] private float _characterSize = 0.012f;

        private TextMesh _text;
        private Vector3 _position;
        private bool _placed;

        public void SetText(string text)
        {
            if (_text != null)
            {
                _text.text = text;
            }
        }

        private void Awake()
        {
            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform;
            }

            var go = new GameObject("Sandbox Text");
            go.transform.SetParent(transform, worldPositionStays: false);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text = go.AddComponent<TextMesh>();
            _text.font = font;
            _text.fontSize = 64;
            _text.characterSize = _characterSize;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = new Color(1f, 1f, 1f, 0.9f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private void LateUpdate()
        {
            if (_head == null)
            {
                return;
            }

            PanelFollow.Follow(
                transform, _head, _distanceMetres, _heightOffsetMetres, _followEaseSeconds, _maxTrailMetres,
                Time.deltaTime, ref _position, ref _placed);
        }
    }
}
