// Copyright 2021, Infima Games. All Rights Reserved.

using System.Linq;
using UnityEngine;

namespace InfimaGames.LowPolyShooterPack
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class Movement : MovementBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Audio Clips")]
        
        [Tooltip("The audio clip that is played while walking.")]
        [SerializeField]
        private AudioClip audioClipWalking;

        [Tooltip("The audio clip that is played while running.")]
        [SerializeField]
        private AudioClip audioClipRunning;

        [Header("Speeds")]

        [SerializeField]
        private float speedWalking = 5.0f;

        [Tooltip("How fast the player moves while running."), SerializeField]
        private float speedRunning = 9.0f;

        [Tooltip("How fast the player moves while crouching."), SerializeField]
        private float speedCrouching = 2.5f;

        [Header("Jump")]

        private float jumpForce;

        [Tooltip("How high the player jumpes"), SerializeField]
        private float jumpHeight = 1.0f;

        [Tooltip("Gravity value"), SerializeField]
        private float gravity = -9.81f;

        [Header("Crouching")]

        [Tooltip("Height of the capsule when crouching"), SerializeField]
        private float crouchHeight = 1.0f;

        

        #endregion

        #region PROPERTIES

        //Velocity.
        private Vector3 Velocity
        {
            //Getter.
            get => rigidBody.velocity;
            //Setter.
            set => rigidBody.velocity = value;
        }

        #endregion

        #region FIELDS

        /// <summary>
        /// Attached Rigidbody.
        /// </summary>
        private Rigidbody rigidBody;
        /// <summary>
        /// Attached CapsuleCollider.
        /// </summary>
        private CapsuleCollider capsule;
        /// <summary>
        /// Attached AudioSource.
        /// </summary>
        private AudioSource audioSource;
        
        /// <summary>
        /// True if the character is currently grounded.
        /// </summary>
        private bool grounded;

        /// <summary>
        /// Player Character.
        /// </summary>
        private CharacterBehaviour playerCharacter;
        /// <summary>
        /// The player character's equipped weapon.
        /// </summary>
        private WeaponBehaviour equippedWeapon;
        
        /// <summary>
        /// Array of RaycastHits used for ground checking.
        /// </summary>
        private readonly RaycastHit[] groundHits = new RaycastHit[8];

        private float verticalVelocity = 0.0f;

        private bool wasRunning = false;

        private bool isCrouching = false;

        private float standHeight = 1.8f;

        #endregion

        #region UNITY FUNCTIONS

        /// <summary>
        /// Awake.
        /// </summary>
        protected override void Awake()
        {
            //Get Player Character.
            playerCharacter = ServiceLocator.Current.Get<IGameModeService>().GetPlayerCharacter();
        }

        /// Initializes the FpsController on start.
        protected override  void Start()
        {
            //Rigidbody Setup.
            rigidBody = GetComponent<Rigidbody>();
            rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
            //Cache the CapsuleCollider.
            capsule = GetComponent<CapsuleCollider>();

            //Audio Source Setup.
            audioSource = GetComponent<AudioSource>();
            audioSource.clip = audioClipWalking;
            audioSource.loop = true;

            // Oblicz siłę skoku na podstawie wysokości skoku
            jumpForce = Mathf.Sqrt(2 * Mathf.Abs(gravity) * jumpHeight);
        }

        /// Checks if the character is on the ground.
        private void OnCollisionStay()
        {
            //Bounds.
            Bounds bounds = capsule.bounds;
            //Extents.
            Vector3 extents = bounds.extents;
            //Radius.
            float radius = extents.x - 0.01f;
            
            //Cast. This checks whether there is indeed ground, or not.
            Physics.SphereCastNonAlloc(bounds.center, radius, Vector3.down,
                groundHits, extents.y - radius * 0.5f, ~0, QueryTriggerInteraction.Ignore);
            
            //We can ignore the rest if we don't have any proper hits.
            if (!groundHits.Any(hit => hit.collider != null && hit.collider != capsule)) 
                return;
            
            //Store RaycastHits.
            for (var i = 0; i < groundHits.Length; i++)
                groundHits[i] = new RaycastHit();

            //Set grounded. Now we know for sure that we're grounded.
            grounded = true;
        }
			
        protected override void FixedUpdate()
        {
            //Move.
            MoveCharacter();
            
            //Unground.
            grounded = false;
        }

        /// Moves the camera to the character, processes jumping and plays sounds every frame.
        protected override  void Update()
        {
            //Get the equipped weapon!
            equippedWeapon = playerCharacter.GetInventory().GetEquipped();
            
            //Play Sounds!
            PlayFootstepSounds();
        }

        #endregion

        #region METHODS

        private void MoveCharacter()
        {
            
            #region Calculate Jump Velocity

            //Check if the player is jumping and is grounded
            if (playerCharacter.IsJumping() && grounded)
            {
                // Set the vertical velocity for the jump
                verticalVelocity = jumpForce;
                // Set grounded to false since the character is now in the air
                grounded = false;
            }
            else
                ApplyGravity();

            #endregion

            #region Calculate Movement Velocity

            //Get Movement Input!
            Vector2 frameInput = playerCharacter.GetInputMovement();

            //Calculate local-space direction by using the player's input.
            var movement = new Vector3(frameInput.x, 0.0f, frameInput.y);

            //Running speed calculation.
            if (playerCharacter.IsRunning() && !isCrouching)
            {
                if (grounded)
                {
                    movement *= speedRunning;
                }else if(!grounded && wasRunning)
                {
                    movement *= speedRunning*0.75f;
                }
                wasRunning = true;
            }
            else if (isCrouching)
            {
                //Multiply by the crouching speed.
                movement *= speedCrouching;
            }
            else
            {
                //Multiply by the normal walking speed.
                movement *= grounded ? speedWalking : speedWalking * 0.5f;
                wasRunning = false;
            }

            //World space velocity calculation. This allows us to add it to the rigidbody's velocity properly.
            movement = transform.TransformDirection(movement);

            #endregion

            #region Calculate Crouch

            if (playerCharacter.IsCrouching())
            {
                if (!isCrouching)
                {
                    // Adjust the capsule height for crouching
                    capsule.height = crouchHeight;
                    isCrouching = true;
                }
            }
            else
            {
                if (isCrouching)
                {
                    // Adjust the capsule height for standing
                    capsule.height = standHeight;
                    isCrouching = false;
                }
            }

            #endregion

            //Update Velocity.
            Velocity = new Vector3(movement.x, verticalVelocity, movement.z);
            playerCharacter.Ground = grounded;
        }


        private void ApplyGravity()
        {
            if (!grounded)
            {
                // Apply gravity to the vertical velocity
                verticalVelocity += gravity * Time.deltaTime;
            }
            else
            {
                // Reset vertical velocity when grounded
                verticalVelocity = 0.0f;
            }
        }

        /// <summary>
        /// Plays Footstep Sounds. This code is slightly old, so may not be great, but it functions alright-y!
        /// </summary>
        private void PlayFootstepSounds()
        {
            //Check if we're moving on the ground. We don't need footsteps in the air.
            if (grounded && rigidBody.velocity.sqrMagnitude > 0.1f && !playerCharacter.IsCrouching())
            {
                //Select the correct audio clip to play.
                audioSource.clip = playerCharacter.IsRunning() ? audioClipRunning : audioClipWalking;
                //Play it!
                if (!audioSource.isPlaying)
                    audioSource.Play();
            }
            //Pause it if we're doing something like flying, or not moving!
            else if (audioSource.isPlaying)
                audioSource.Pause();
        }

        #endregion
    }
}