    using UnityEngine;
    using MelonLoader;
    using Il2Cpp;
    using Il2CppDecaGames.RotMG.Managers;
    using Il2CppDecaGames.RotMG.UI.Buttons;
    using UnityEngine.UI;

namespace Follower
    {
    public class follow : MelonMod
    {

    // Gameplay
    private string lastTargetName = "";
        private bool isFollowing = false;

        // status flags
        private bool canShowFollowingMessage = true;
        private bool hasReacquired = false;
        private static bool floatingtext = false;

        // scene and management
        private GameObject? gameControllerObj;
        private ApplicationManager? applicationManagerObj;
        private INOGPMEIHLB? sceneInformation;

        // player and movement
        private GameObject player;
        private GameObject targetPlayer;
        private bool isMovingTowardsTarget = false;
        private float speed = 10f;
        private bool manualMovementDetected = false;

        // portals and interaction
        private Vector3? portalPosition = null;
        private string[] restrictedPortalNames = { "Vault", "Pet Yard", "Daily Quest Room" };
        private bool shouldEnterPortal = false;
        private bool targetLost = false;
        private HashSet<string> loggedKeys = new HashSet<string>();
        private Dictionary<Vector3, string> activePortals = new Dictionary<Vector3, string>();

        // collision
        private HashSet<Vector3> collisionPositions = new HashSet<Vector3>();
        private Vector3? lastLoggedCollisionPosition = null;
        private bool collisionInRangeLogged = false;
        private const float CollisionPush = 0.1f;



    // Call all functions
         public override void OnUpdate()
         {           
            HandleInput();
            HandleTargetPosition();
            MovePlayer();
            StoreCollision();
            HandlePortalFollowing();
            ReacquireAndFollowTarget();
            ProcessCommandMessages();
            HandlePlayerInitialization();
        }



        // initialization 
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Main")
            {
                SetupGameObjects();
            }
        }

        private void HandlePlayerInitialization()
        {
            if (player == null)
            {
                player = GameObject.Find("Player/Player");
            }
        }

        private void SetupGameObjects()
        {
            gameControllerObj = GameObject.Find("GameController");

            applicationManagerObj = gameControllerObj?.GetComponent<ApplicationManager>();

            sceneInformation = applicationManagerObj?._JLCMKLPBLMF_k__BackingField;


            if (player == null)
            {
                player = GameObject.Find("Player/Player");
            }
        }

        // self movement (hardcoded to WASD, if you want to change keybinds you can create a func to take horizontal + vertical inputs, you will need to account for slide)       
        private void HandleInput()
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))
            {
                isMovingTowardsTarget = false;
            }
            else if (!string.IsNullOrEmpty(lastTargetName))
            {
                isMovingTowardsTarget = true;
            }

        }



        // Chat follow and speed
        private void ProcessCommandMessages()
        {
            GameObject chatGui = GameObject.Find("Chat_GUI");
            if (chatGui == null)
            {
                return;
            }

            Transform chatTransform = chatGui.transform.Find("Chat/Output Scroll View/Viewport/Output");
            if (chatTransform == null)
            {
                return;
            }

            for (int i = 0; i < chatTransform.childCount; i++)
            {
                Transform child = chatTransform.GetChild(i);
                if (child.name == "AnyMessage" && child.gameObject.activeInHierarchy)
                {
                    Transform messageChild = child.Find("Message");
                    if (messageChild != null && messageChild.gameObject.activeInHierarchy)
                    {
                        var textComponent = messageChild.GetComponent<Il2CppTMPro.TextMeshProUGUI>();
                        if (textComponent != null)
                        {
                            string content = textComponent.text.ToLower();

                         
                            if (content.StartsWith("<color=#ffff00>unrecognized command: /follow "))
                            {
                                string targetName = content.Replace("<color=#ffff00>unrecognized command: /follow ", "").Replace("</color>", "").Trim();
                                textComponent.text = $"<color=#00AF00>Following {targetName}</color>";
                                lastTargetName = targetName;
                                SetTargetPlayer(true);

                                if (canShowFollowingMessage)
                                {
                                    ShowFloatingText($"Following {lastTargetName}", true);
                                    canShowFollowingMessage = false; 
                                }
                            }
                          
                            else if (content.StartsWith("<color=#ffff00>unrecognized command: /speed "))
                            {
                                string speedValue = content.Replace("<color=#ffff00>unrecognized command: /speed ", "").Replace("</color>", "").Trim();
                                if (float.TryParse(speedValue, out float newSpeed))
                                {
                                    speed = newSpeed;
                                    textComponent.text = $"<color=#00AF00>Speed set to {newSpeed}</color>";
                                }
                                else
                                {
                                    textComponent.text = $"<color=#FF0000>Invalid speed value</color>";
                                }
                            }
                        }
                    }
                }
            }
        }



        // Portal entry
        private void AttemptPortalEntry()
        {
            GameObject menuButton = GameObject.Find("Interaction_GUI/Interaction Panel/Interaction Buttons/Menu_Button");
            if (menuButton != null && menuButton.activeInHierarchy)
            {
                AttemptClick<BaseButton>(menuButton, "BaseButton");
            }
        }

        private void AttemptClick<T>(GameObject obj, string componentName) where T : Component
        {
            T component = obj.GetComponent<T>();
            if (component != null)
            {
                Button btn = component as Button;
                if (btn != null && btn.interactable)
                {
                    btn.onClick.Invoke();

                }
            }
        }

        private void HandleTargetPosition()
        {
            if (targetPlayer == null && portalPosition.HasValue)
            {
                player.transform.position = portalPosition.Value;

                GameObject menuButton = GameObject.Find("Interaction_GUI/Interaction Panel/Interaction Buttons/Menu_Button");
                if (menuButton != null && menuButton.activeInHierarchy)
                {
                    AttemptPortalEntry();
                    portalPosition = null;
                }
                return;
            }

            if (targetPlayer == null && !string.IsNullOrEmpty(lastTargetName))
            {
                ReacquireTarget();
            }

            if (targetPlayer != null && !targetPlayer.activeSelf)
            {
                StopFollowingTarget();
            }
        }
        private void HandlePortalFollowing()
        {
            if (sceneInformation == null || sceneInformation.IEBGLNKIIMB == null)
            {
                return;
            }

            foreach (var entry in sceneInformation.IEBGLNKIIMB)
            {
                if (!loggedKeys.Contains(entry.Key.ToString()))
                {
                    if (entry.Value.ToString() == "ECJIPGJGJGJ")
                    {
                        var portal = entry.Value as dynamic;
                        if (portal != null)
                        {
                            string portalName = portal.PPOIPKLADIA;


                            float adjustedY = -portal.AFEPKLFLJEJ.y;
                            if (!restrictedPortalNames.Contains(portalName)) 
                            {
                                Vector3 portalPosition = new Vector3(portal.AFEPKLFLJEJ.x, adjustedY);
                                activePortals[portalPosition] = portalName; 
                            }
                        }
                    }

                    loggedKeys.Add(entry.Key.ToString());
                }
            }
        }



        // Floating text - credits to Him, Pog, Smol.
        public void ShowFloatingText(String text, Color32 color)
        {


            Il2CppSystem.Nullable<Color32> newColor = new Il2CppSystem.Nullable<Color32>(color);

            MPFFENCAICI effectType = MPFFENCAICI.Xp;

            if (!floatingtext)
            {
                for (int i = 0; i < 12; i++)
                {
                    sceneInformation?.HMBPMEGLIKJ.BCJJIBCCFLI.iGUIManager.ShowFloatingText(effectType, "", newColor, 0.0f, 0.0f,
                        0.0f);
                }
                floatingtext = true;
            }

            sceneInformation?.HMBPMEGLIKJ.BCJJIBCCFLI.iGUIManager.ShowFloatingText(effectType, text, newColor, 0.0f, 0.0f, 0.0f);
            sceneInformation?.HMBPMEGLIKJ.JMMBCMBPCBF(color);
        }


        public void ShowFloatingText(String text, bool? toggle = null)
        {
            var color = toggle switch
            {
                true => new Color32(32, 220, 0, 255), 
                false => new Color32(255, 0, 25, 255), 
                _ => new Color32(220, 220, 220, 255)
            };

            ShowFloatingText(text, color);
        }



        // Collisions


        private void StoreCollision()
        {
            if (sceneInformation?.IEBGLNKIIMB == null) return;
            foreach (var entry in sceneInformation.IEBGLNKIIMB)
            {
                var enemy = entry.Value;
                if (enemy?.CIGEHLNDCJL?.occupySquare == true)
                {
                    enemy.CIGEHLNDCJL.ignoreHit = true;
                    AddCollisionPosition(entry.Key.ToString(), new Vector3(enemy.NGGPLFAJAAL, -enemy.NFMCMENGEME, 0f));
                }
            }
        }

        private void AddCollisionPosition(string enemyName, Vector3 enemyPosition)
        {
            if (!collisionPositions.Contains(enemyPosition))
            {

                collisionPositions.Add(enemyPosition);
            }
        }

        private bool IsNearCollision(Vector3 checkPosition)
        {
            foreach (var collisionPosition in collisionPositions)
            {
                float distance = Vector3.Distance(checkPosition, collisionPosition);
                if (distance < 0.9f)
                {
                    if (!lastLoggedCollisionPosition.HasValue || lastLoggedCollisionPosition.Value != collisionPosition)
                    {

                        lastLoggedCollisionPosition = collisionPosition;
                    }
                    return true;
                }
            }

            lastLoggedCollisionPosition = null;
            return false;
        }



        // Player and Character
        private GameObject FindPlayer(string targetNameLower)
        {
            GameObject characterObject = GameObject.Find("Character");
            if (characterObject == null) return null;

            Transform characterTransform = characterObject.GetComponent<Transform>();
            if (characterTransform == null) return null;

            for (int i = 0; i < characterTransform.childCount; i++)
            {
                Transform child = characterTransform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;

                Il2CppTMPro.TextMeshPro tmp = child.GetComponentInChildren<Il2CppTMPro.TextMeshPro>();
                if (tmp == null) continue;

                string playerName = System.Text.RegularExpressions.Regex.Replace(tmp.text, "<.*?>", string.Empty).ToLower();
                if (playerName == targetNameLower)
                {
                    return child.gameObject;
                }
            }

            return null;
        }
   
        private void ReacquireAndFollowTarget()
        {
            if (string.IsNullOrEmpty(lastTargetName) || hasReacquired)
            {
                return;
            }

            targetPlayer = FindPlayer(lastTargetName.ToLower());

            if (targetPlayer != null)
            {

                isMovingTowardsTarget = true;
                hasReacquired = true;
            }
        }

        private void StopFollowingTarget()
        {
            if (targetPlayer != null)
            {
                Vector3 targetPosition = targetPlayer.transform.position;
                ShowFloatingText("Stopped Following", false);

                foreach (var portal in activePortals)
                {
                    float distanceToPortal = Vector3.Distance(targetPosition, portal.Key);
                    if (distanceToPortal < 1.5f)
                    {
                        portalPosition = portal.Key;
                        shouldEnterPortal = true;
                        break;
                    }
                }

                isMovingTowardsTarget = false;
                targetPlayer = null;
                isFollowing = false; 
            }
        }

        private void SetTargetPlayer(bool skipInput = false)
        {
            targetPlayer = null;

            if (!skipInput)
            {

                string targetNameInput = System.Console.ReadLine().Trim();
                lastTargetName = targetNameInput;
            }

            targetLost = true;

            targetPlayer = FindPlayer(lastTargetName.ToLower());
        }

        private void ReacquireTarget()
        {
            if (string.IsNullOrEmpty(lastTargetName)) return;

            targetPlayer = FindPlayer(lastTargetName.ToLower());
            if (targetPlayer != null)
            {
                if (!isFollowing)
                {
                    ShowFloatingText($"Following {lastTargetName}", true);
                    isFollowing = true;
                }
                isMovingTowardsTarget = true;
            }
            else
            {
                isMovingTowardsTarget = false;
            }
        }



        // Repath system when player hits a collision 
        private Vector3 DetermineSidestepDirection(Vector3 currentPosition, Vector3 targetPosition)
        {
            Vector3 desiredDirection = (targetPosition - currentPosition).normalized;
            Vector3 sidestepLeft = new Vector3(-desiredDirection.y, desiredDirection.x, 0).normalized;
            Vector3 sidestepRight = new Vector3(desiredDirection.y, -desiredDirection.x, 0).normalized;

            if (!IsNearCollision(currentPosition + sidestepLeft))
                return sidestepLeft;
            else if (!IsNearCollision(currentPosition + sidestepRight))
                return sidestepRight;
            else
                return Vector3.zero;
        }



        // Movement 
        private void MovePlayer()
        {
            if (manualMovementDetected || !isMovingTowardsTarget) return;

            var viewHandler = player.GetComponent<Il2CppDecaGames.RotMG.Objects.Map.Data.ViewHandler>();
            if (viewHandler == null) return;

            var destroyEntity = viewHandler.destroyEntity;
            var playerData = destroyEntity.Cast<KCHCBJBKCAA>();
            if (playerData == null) return;

            Vector3 currentPlayerPosition = new Vector3(playerData.NGGPLFAJAAL, -playerData.NFMCMENGEME, 0);
            if (targetPlayer == null) return;

            Vector3 targetPosition = targetPlayer.transform.position;

            float distanceToTarget = Vector3.Distance(currentPlayerPosition, targetPosition);
            if (distanceToTarget < 0.3f) return;

            Vector3 desiredDirection = (targetPosition - currentPlayerPosition).normalized;
            Vector3 nextPosition = currentPlayerPosition + desiredDirection * speed * Time.deltaTime;

            if (IsNearCollision(nextPosition))
            {
                Vector3 sidestepDirection = DetermineSidestepDirection(currentPlayerPosition, targetPosition);
                if (sidestepDirection != Vector3.zero)
                {
                    nextPosition = currentPlayerPosition + sidestepDirection * speed * Time.deltaTime;
                }
                else
                {
                 
                    nextPosition -= desiredDirection * CollisionPush;
                    return;
                }
            }

            playerData.NGGPLFAJAAL = nextPosition.x;
            playerData.NFMCMENGEME = -nextPosition.y;
            playerData.MNOAPMOOBKB = new Vector3(nextPosition.x, nextPosition.y, 0);
        }
    }
}
