using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// How a floating text panel (<see cref="LessonHud"/>, <see cref="MenuView"/>, <see cref="TutorialView"/>)
    /// stays in the learner's view (ADR-0056): level with the head's heading, a set distance in front of it, turned
    /// to face it. The three used to carry the same few lines each.
    ///
    /// <para>
    /// The follow is soft, not head-locked - rigid geometry glued to the head is uncomfortable (ADR-0020) - so the
    /// panel eases toward where it belongs. But an ease alone has no upper bound on how far behind it can be: a
    /// panel that was placed before the headset's pose had settled, or left where it was while it was switched off,
    /// or a head that has turned quickly, would all take a second or two to glide into view. So the ease is
    /// <b>leashed</b>: the panel never trails the point it belongs at by more than <c>maxTrailMetres</c>. Within the
    /// leash it eases as before; beyond it, it is carried along at the head's own pace, so it is never far out of view.
    /// </para>
    /// </summary>
    internal static class PanelFollow
    {
        /// <summary>
        /// Move <paramref name="panel"/> toward its place in front of <paramref name="head"/> by one frame.
        /// <paramref name="position"/> is the panel's eased position and <paramref name="placed"/> whether it has one
        /// yet; clear <paramref name="placed"/> to put the panel straight into place on the next call.
        /// </summary>
        public static void Follow(
            Transform panel,
            Transform head,
            float distanceMetres,
            float heightMetres,
            float easeSeconds,
            float maxTrailMetres,
            float deltaSeconds,
            ref Vector3 position,
            ref bool placed)
        {
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                return; // looking straight up or down: there is no heading to follow
            }

            forward.Normalize();
            Vector3 wanted = head.position + forward * distanceMetres + Vector3.up * heightMetres;
            position = placed ? Ease(position, wanted, deltaSeconds, easeSeconds, maxTrailMetres) : wanted;
            placed = true;

            panel.position = position;
            panel.rotation = Quaternion.LookRotation(position - head.position, Vector3.up);
        }

        /// <summary>
        /// One step of the leashed ease: an exponential approach to <paramref name="wanted"/> with time constant
        /// <paramref name="easeSeconds"/> (0 = jump), then pulled in to within <paramref name="maxTrailMetres"/> of it.
        /// </summary>
        internal static Vector3 Ease(
            Vector3 current, Vector3 wanted, float deltaSeconds, float easeSeconds, float maxTrailMetres)
        {
            float k = easeSeconds <= 0f ? 1f : 1f - Mathf.Exp(-deltaSeconds / easeSeconds);
            Vector3 next = Vector3.Lerp(current, wanted, k);

            Vector3 trail = wanted - next;
            float trailLength = trail.magnitude;
            if (trailLength > maxTrailMetres && trailLength > 0f)
            {
                next = wanted - trail * (maxTrailMetres / trailLength);
            }

            return next;
        }
    }
}
