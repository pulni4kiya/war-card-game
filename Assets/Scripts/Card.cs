using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour {
	public Image faceImage;
	public Image backImage;

	[NonSerialized]
	public CardRank rank;
	[NonSerialized]
	public CardSuit suit;
	[NonSerialized]
	public int power;
}
