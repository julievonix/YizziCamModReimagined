using UnityEngine;
#pragma warning disable CS0618
namespace YizziCamModV2.Comps
{
    class RightGrabTrigger : MonoBehaviour
    {
        void Start()
        {
            gameObject.layer = 18;
        }
        void OnTriggerStay(Collider col)
        {
            bool holding = InputManager.instance.RightGrip || InputManager.instance.RightTrigger;
            if (col.name.Contains("Right"))
            {
                if (holding & (!CameraController.Instance.fpv || CameraController.Instance.camDisconnect))
                {
                    CameraController.Instance.CameraTablet.transform.parent = CameraController.Instance.RightHandGO.transform;
                    if (CameraController.Instance.fp)
                    {
                        CameraController.Instance.fp = false;
                        if (CameraController.Instance.flipped)
                        {
                            CameraController.Instance.flipped = false;
                            CameraController.Instance.ThirdPersonCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                            CameraController.Instance.TabletCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                        }
                        CameraController.Instance.ResetTabletCamera();
                    }
                }
            }
            if (!holding & CameraController.Instance.CameraTablet.transform.parent == CameraController.Instance.RightHandGO.transform)
            {
                CameraController.Instance.CameraTablet.transform.parent = null;
            }
        }
    }
}
