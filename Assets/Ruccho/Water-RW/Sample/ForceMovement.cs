using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ruccho
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class ForceMovement : MonoBehaviour
    {
        [SerializeField]
        private float power = 15f;
        [SerializeField]
        private float jumpForce = 8f;

        private Rigidbody2D rigidbody2DRef;
        private Vector2 UpdateForce;

        // Start is called before the first frame update
        void Start()
        {
            rigidbody2DRef = GetComponent<Rigidbody2D>();
        }

        // Update is called once per frame
        void Update()
        {
            float horizontal = Input.GetAxis("Horizontal");
            UpdateForce = Vector2.right * horizontal * power;

            if(Input.GetButtonDown("Jump"))
            {
                rigidbody2DRef.velocity = new Vector2(rigidbody2DRef.velocity.x, 0);
                rigidbody2DRef.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            }
        }

        private void FixedUpdate()
        {
            rigidbody2DRef.AddForce(UpdateForce);
        }
    }
}