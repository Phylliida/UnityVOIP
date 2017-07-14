/* 
 * Copyright (C) 2015 Christoph Kutza
 * 
 * Please refer to the LICENSE file for license information
 */
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Shows a list of a text prefab.
/// 
/// Used to show the messages that are sent/received in the ChatApp.
/// </summary>
public class MessageList : MonoBehaviour
{
    /// <summary>
    /// References to the "Text" prefab.
    /// 
    /// Needs to contain RectTransform and Text element.
    /// </summary>
    public GameObject uEntryPrefab;



    /// <summary>
    /// Reference to the own rect transform
    /// </summary>
    private RectTransform mOwnTransform;

    /// <summary>
    /// Number of messages until the older messages will be deleted.
    /// </summary>
    private int mMaxMessages = 50;


    private int mCounter = 0;

    private void Awake()
    {
        mOwnTransform = this.GetComponent<RectTransform>();
    }

    private void Start()
    {
        foreach(var v in mOwnTransform.GetComponentsInChildren<RectTransform>())
        {
            if(v != mOwnTransform)
            {
                v.name = "Element " + mCounter;
                mCounter++;
            }
        }
    }

    /// <summary>
    /// Allows the Chatapp to add new entires to the list
    /// </summary>
    /// <param name="text">Text to be added</param>
    public void AddTextEntry(string text)
    {
        GameObject ngp = Instantiate(uEntryPrefab);
        Text t = ngp.GetComponentInChildren<Text>();
        t.text = text;

        RectTransform transform = ngp.GetComponent<RectTransform>();
        transform.SetParent(mOwnTransform, false);

        GameObject go = transform.gameObject;
        go.name = "Element " + mCounter;
        mCounter++;
    }
    

    /// <summary>
    /// Destroys old messages if needed and repositions the existing messages.
    /// </summary>
    private void Update()
    {
        int destroy = mOwnTransform.childCount - mMaxMessages;
        for(int i = 0; i < destroy; i++)
        {
            var child = mOwnTransform.GetChild(i).gameObject;
            Destroy(child);
        }
    }

}
