﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using SgLib;

public class PlayerController : MonoBehaviour
{
    public static event System.Action PlayerDied;
    public static event System.Action PlayerJumped;

    public static bool isFirstLoad = true;
    public static int gameCountForRewardedAds = 0;

    [Header("Gameplay Config")]
    public int minPlaneNumber = 5;
    // Should be 5 or bigger to create a nice path
    public int maxPlaneNumber = 10;
    public float planeDistance = 3f;
    //The distance from a plane to next plane
    public float yPosition = 8f;
    //This is the first y position of plane when it's created
    public float destroyPlaneTime = 0.8f;
    public float movePlaneAndCoinTime = 0.5f;
    public int scoreToUpdateValue = 25;
    // hide the two colored buttons after reaching this number of jumps
    public int initialPlanes = 4;
    // number of planes created at startup

    [Range(0, 1)]
    public float coinFrequency = 0.1f;

    [HideInInspector]
    public bool isCameraFollow = true;
    [HideInInspector]
    public bool isGameOver = false;

    [Header("Object References")]
    public UIManager UIManager;
    public GameObject parentPlayer;
    public GameObject firstPlane;
    public GameObject yellowPlanePrefab;
    public GameObject redPlanePrefab;
    public GameObject coinPrefab;
    public ParticleSystem particle;
    public Animator anim;
    public AnimationClip jumpAnimation;

    private Vector3 currentPlanePosition;
    private Vector3 currentDirection;
    private GameObject currentPlane;
    private GameObject currentCoin;
    private int planeNumber;
    private int redOrYellow;
    private int count = 0;
    private int indexOfList = -1;
    private int currentPlaneIndex = 0;
    private bool checkToStartDestroy = false;
    private bool isPlayerTurnRight;
    private bool isRedButtonHitted = false;
    private bool isYellowButtonHitted = false;
    private bool enableJump = false;
    private int jumpCount = 0;
    private float SpeedPlaneFalling = 20f;
    private List<GameObject> listPlane = new List<GameObject>();
    private Vector3 originalPos;

    // Use this for initialization
    void Start()
    {
        // Switch to the currently selected character
        GameObject currentCharacter = CharacterManager.Instance.characters[CharacterManager.Instance.CurrentCharacterIndex];
        Mesh charMesh = currentCharacter.GetComponent<MeshFilter>().sharedMesh;
        Material charMaterial = currentCharacter.GetComponent<Renderer>().sharedMaterial;
        gameObject.GetComponent<MeshFilter>().mesh = charMesh;
        gameObject.GetComponent<MeshRenderer>().material = charMaterial;

        // Make the player fall down from above
        originalPos = transform.parent.transform.position;
        transform.parent.transform.position += new Vector3(0, 30, 0); 
        transform.parent.transform.rotation = Quaternion.Euler(0, 180, 0);

        // Add first plane to plane list
        listPlane.Add(firstPlane);

        // Create initial planes
        currentDirection = Vector3.right;
        planeNumber = Random.Range(minPlaneNumber, maxPlaneNumber);
        currentPlanePosition = firstPlane.transform.position;

        for (int i = 0; i < initialPlanes; i++)
        {
            CreatePlane(false);
        }

        isPlayerTurnRight = true;

        enableJump = false;
        StartCoroutine(WaitToDestroy());

        if (!isFirstLoad)
        {
            StartGame();
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && enableJump && GameManager.Instance.GameState == GameState.Playing)
        {
            Vector3 mousePos = Input.mousePosition;

            // A click/tap on the right half of the screen is considered as a "RedButton" click,
            // a click/tap on the other half it is considered as a "YellowButton" click.
            if (mousePos.x >= Screen.width / 2)
            {
                StartCoroutine(ButtonClick("RedButton"));
            }
            else
            {
                StartCoroutine(ButtonClick("YellowButton"));
            }
        }
    }

    public void StartGame()
    {
        isFirstLoad = false;
        StartCoroutine(CRStartGame());
    }

    IEnumerator CRStartGame()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = originalPos;
        float runTime = 0.5f;
        float timePast = 0f;

        while (timePast < runTime)
        {
            timePast += Time.deltaTime;
            float factor = timePast / runTime;
            transform.parent.position = Vector3.Lerp(startPos, endPos, factor);
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        enableJump = true;
    }

    public void RestartGame(float delay = 0)
    {
        StartCoroutine(CRRestart(delay));
    }

    IEnumerator CRRestart(float delay = 0)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GameOver()
    {
        if (isGameOver)
            return;

        anim.SetTrigger("Die");

        SoundManager.Instance.PlaySound(SoundManager.Instance.gameOver);

        isGameOver = true;
        enableJump = false;
        isCameraFollow = false;

        // Fire event
        if(PlayerDied != null)
            PlayerDied();
    }

    void CreatePlane(bool allowCreatingGold = true)
    {
        count++;
        redOrYellow = Random.Range(0, 2);
        GameObject prefab;

        if (redOrYellow == 0) //Create red plane
        {
            prefab = redPlanePrefab;
        }
        else //Create yellow plane
        {
            prefab = yellowPlanePrefab;
        }

        if (count >= planeNumber) //Last plane
        {
            if (currentDirection == Vector3.right)
            {
                currentDirection = Vector3.forward;
            }
            else
            {
                currentDirection = Vector3.right;
            }
            count = 0;
            planeNumber = Random.Range(minPlaneNumber, maxPlaneNumber);
        }

        currentPlanePosition += currentDirection * planeDistance;
        currentPlane = (GameObject)Instantiate(prefab, currentPlanePosition, Quaternion.identity);

        if (count == planeNumber - 1)
        {
            currentPlane.GetComponent<PlaneController>().isTheLastPlane = true;
        }

        listPlane.Add(currentPlane);

        currentPlane.transform.position += new Vector3(0, -yPosition, 0); //Make it at bottom and move it to top

        // Generate golds (gems)
        if (allowCreatingGold)
        {
            float coinProbability = Random.Range(0f, 1f);
            if (coinProbability <= coinFrequency)
            {
                float planeLocalScale = redPlanePrefab.transform.localScale.y / 2;

                //Create coin
                currentCoin = (GameObject)Instantiate(coinPrefab, (currentPlane.transform.position + new Vector3(0, 0.5f, 0)) + Vector3.up * planeLocalScale, Quaternion.identity);
                StartCoroutine(MoveCoin(currentCoin, movePlaneAndCoinTime));//Move coin up
            }
        }

        StartCoroutine(MovePlane(currentPlane, movePlaneAndCoinTime));//Move plane up
    }

    void WaitTimeReduce()
    {
        float bottomLimit = jumpAnimation.length + 0.05f;
        if (destroyPlaneTime > bottomLimit)
        {
            if (ScoreManager.Instance.Score % scoreToUpdateValue == 0 && ScoreManager.Instance.Score != 0)
            {
                destroyPlaneTime = destroyPlaneTime - 0.05f;
            }
        }
        else
        {
            // The jump animation takes some time to complete, so we should have a bottom limit
            // of how fast the planes are destroyed, otherwise game may become impossible.
            destroyPlaneTime = bottomLimit;
        }
    }

    IEnumerator ButtonClick(string buttonName)
    {
        if (enableJump)
        {
            enableJump = false; //Disable jump
            checkToStartDestroy = true; // Start to destroy plane
            currentPlaneIndex++;

            if (jumpCount == 0)
            {
                // Make the character face forward
                Transform parentTransform = transform.parent;
                Quaternion startRotation = parentTransform.rotation;
                Quaternion endRotation = Quaternion.Euler(0, 90, 0);
                float rotateTime = 0.1f;
                float timePast = 0;

                while (timePast < rotateTime)
                {
                    timePast += Time.deltaTime;
                    parentTransform.rotation = Quaternion.Lerp(startRotation, endRotation, timePast / rotateTime);
                    yield return null;
                }
            }

            jumpCount++;
            SpeedPlaneFalling += jumpCount;

            if (buttonName == "RedButton")//Check this button is red or yellow
            {
                isRedButtonHitted = true;
                isYellowButtonHitted = false;
            }
            else
            {
                isRedButtonHitted = false;
                isYellowButtonHitted = true;
            }

            if(PlayerJumped != null)
                PlayerJumped();
            CreatePlane(); //Create plane
            WaitTimeReduce();//Check to decrease destroyPlanetime -> plane destroy faster

            anim.Play(jumpAnimation.name); //Jump
            SoundManager.Instance.PlaySound(SoundManager.Instance.jump);
            StartCoroutine(MovePlayer(jumpAnimation.length)); //Move player
        }

        yield return null;
    }

    //When animation "jump" of player end,
    public void OnAnimationEnd()
    {
        if ( //Game over
            (listPlane[currentPlaneIndex].tag == "YellowPlane" && isRedButtonHitted)
            ||
            (listPlane[currentPlaneIndex].tag == "RedPlane" && isYellowButtonHitted))
        {
            destroyPlaneTime = 0.05f;
            GameOver();
            StartCoroutine(WaitAndDisablePlayer(1f));
        }
        else //Not game over
        {
            ScoreManager.Instance.AddScore(1);

            //Set jump direction and enable jump
            if (listPlane[currentPlaneIndex].GetComponent<PlaneController>().isTheLastPlane)
            {
                if (isPlayerTurnRight)
                {
                    isPlayerTurnRight = false;
                    gameObject.transform.parent.transform.rotation = Quaternion.Euler(0, 0, 0);
                }
                else
                {
                    isPlayerTurnRight = true;
                    gameObject.transform.parent.transform.rotation = Quaternion.Euler(0, 90, 0);
                }
            }

            enableJump = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Coin")
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.hitItem);
            CoinManager.Instance.AddCoins(1);
            ParticleSystem particleTemp = (ParticleSystem)Instantiate(particle, other.transform.position, Quaternion.Euler(-90, 0, 0));
            particleTemp.Play();
            Destroy(particleTemp, 1f);
            Destroy(other.gameObject);
        }
    }

    IEnumerator WaitToDestroy()
    {
        while (true)
        {
            if (checkToStartDestroy == true)
            {

                yield return new WaitForSeconds(destroyPlaneTime);
                indexOfList++;
                listPlane[indexOfList].GetComponent<Rigidbody>().isKinematic = false;
                listPlane[indexOfList].GetComponent<Rigidbody>().velocity = Vector3.down * SpeedPlaneFalling;
                Destroy(listPlane[indexOfList], 3f);

                if (indexOfList == listPlane.Count - 4) //Player standing in this plane -> game over
                {
                    destroyPlaneTime = 0.05f;
                    GetComponent<Rigidbody>().isKinematic = true;
                    GameOver();
                    StartCoroutine(WaitAndDisablePlayer(1f));
                   
                }
                if (indexOfList == listPlane.Count - 1) //this is last plane
                {
                    yield break;
                }
            }
            yield return null;
        }
    }

    IEnumerator MovePlayer(float timeRun)
    {

        Vector3 startPos = parentPlayer.transform.position;
        Vector3 endPos = (isPlayerTurnRight) ?
                        parentPlayer.transform.position + new Vector3(planeDistance, 0, 0) :
                        parentPlayer.transform.position + new Vector3(0, 0, planeDistance);
        
        float t = 0;
        while (t < timeRun)
        {
            t += Time.deltaTime;
            float fraction = t / timeRun;
            parentPlayer.transform.position = Vector3.Lerp(startPos, endPos, fraction);        
            yield return null;
        }
    }


    IEnumerator MovePlane(GameObject plane, float timeMove)
    {
        Vector3 startPos = plane.transform.position;
        Vector3 endPos = plane.transform.position + new Vector3(0, yPosition, 0);
        float t = 0;
        while (t < timeMove)
        {
            float fraction = t / timeMove;
            plane.transform.position = Vector3.Lerp(startPos, endPos, fraction);
            t += Time.deltaTime;
            yield return null;
        }
        plane.transform.position = endPos;
    }


    IEnumerator MoveCoin(GameObject coin, float timeMove)
    {
        if (coin != null)
        {
            Vector3 startPos = coin.transform.position;
            Vector3 endPos = coin.transform.position + new Vector3(0, yPosition, 0);

            float t = 0;
            while (t < timeMove)
            {
                t += Time.deltaTime;
                float fraction = t / timeMove;
                coin.transform.position = Vector3.Lerp(startPos, endPos, fraction);
                yield return null;
            }
        }
        else
        {
            yield break;
        }
    }

    IEnumerator WaitAndDisablePlayer(float delay)
    {
        yield return new WaitForSeconds(delay);
        GetComponent<MeshRenderer>().enabled = false;
    }
}
