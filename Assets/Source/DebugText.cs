using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class DebugText : MonoBehaviour
{
    public Text debugText;

    private float m_UpdateShowDeltaTime;//更新帧率的时间间隔;  
    private int m_FrameUpdate = 0;//帧数;  
    private float m_FPS = 0;//帧率

    public Button MoveForwardBtn;
    public Button MoveBackwardBtn;
    public Button MoveLeftBtn;
    public Button MoveRightBtn;
    public Button TurnUpBtn;
    public Button TurnDownBtn;
    public Button TurnLeftBtn;
    public Button TurnRightBtn;
    public Button ResetBtn;

    public Camera mCamera;

    public Camera TinyMapCam;

    public float moveSpeed;

    public float turnSpeed;
    private void Start()
    {
        MoveForwardBtn.onClick.AddListener(MoveForward);
        MoveBackwardBtn.onClick.AddListener(MoveBackward);
        MoveLeftBtn.onClick.AddListener(MoveLeft);
        MoveRightBtn.onClick.AddListener(MoveRight);
        TurnDownBtn.onClick.AddListener(TurnDown);
        TurnLeftBtn.onClick.AddListener(TurnLeft);
        TurnRightBtn.onClick.AddListener(TurnRight);
        TurnUpBtn.onClick.AddListener(TurnUp);
        ResetBtn.onClick.AddListener(Reset);
    }

    private void Reset()
    {
        Vector3 angle = mCamera.transform.eulerAngles;
        angle.x = 0;
        mCamera.transform.eulerAngles = angle;
        Vector3 pos = mCamera.transform.position;
        pos.y = 65;
        mCamera.transform.position = pos;
    }

    private void MoveForward()
    {
        Vector3 dir = mCamera.transform.forward;
        mCamera.transform.position += dir * moveSpeed;
    }

    private void MoveBackward() {
        Vector3 dir = mCamera.transform.forward;
        mCamera.transform.position -= dir * moveSpeed;
    }

    private void MoveLeft() {
        Vector3 dir = mCamera.transform.forward;
        dir = Vector3.Cross(dir, Vector3.up);
        mCamera.transform.position += dir * moveSpeed;
    }

    private void MoveRight() {
        Vector3 dir = mCamera.transform.forward;
        dir = Vector3.Cross(dir, Vector3.up);
        mCamera.transform.position -= dir * moveSpeed;
    }

    private void TurnUp() {
        mCamera.transform.eulerAngles -= new Vector3(turnSpeed, 0, 0);
    }
    private void TurnDown() {
        mCamera.transform.eulerAngles += new Vector3(turnSpeed, 0, 0);
    }
    private void TurnLeft() {
        mCamera.transform.eulerAngles -= new Vector3(0, turnSpeed, 0);
    }
    private void TurnRight() {
        mCamera.transform.eulerAngles += new Vector3(0, turnSpeed, 0);
    }

    // Update is called once per frame
    void Update()
    {
        m_FrameUpdate++;
        m_UpdateShowDeltaTime += Time.deltaTime;
        if (m_UpdateShowDeltaTime >= 0.2)
        {
            m_FPS = m_FrameUpdate / m_UpdateShowDeltaTime;
            m_UpdateShowDeltaTime = 0;
            m_FrameUpdate = 0;
            debugText.text = "FPS:"+(m_FPS.ToString()) + "|" + mCamera.transform.position + "|"+ mCamera.transform.eulerAngles;
        }

        //TinyMapCam.transform.position = mCamera.transform.position + new Vector3(0,10,0);
    }
}
