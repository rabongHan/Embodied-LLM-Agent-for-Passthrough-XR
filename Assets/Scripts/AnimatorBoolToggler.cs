using UnityEngine;

[DisallowMultipleComponent]
public class AnimatorBoolToggler : MonoBehaviour
{
    [Tooltip("The Animator whose parameter you want to drive.")]
    public Animator animator;

    [Tooltip("The name of the bool parameter in your Animator.")]
    public string parameterName = "pickedUp";

    /// <summary>Call this to set the bool _true_</summary>
    public void SetPickedUpTrue()
    {
        if (animator != null)
        {
            animator.SetBool(parameterName, true);
            Debug.Log("setBool true");
        }
    }

    /// <summary>Call this to set the bool _false_</summary>
    public void SetPickedUpFalse()
    {
        if (animator != null)
        {
            animator.SetBool(parameterName, false);
            Debug.Log("setBool false");
        }
    }
}
