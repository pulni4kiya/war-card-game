using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ApplicationSettings {
	public static event EventHandler Changed;

	public static string CardFaces {
		get {
			return PlayerPrefs.GetString("CardFaces", "deck1");
		}
		set {
			PlayerPrefs.SetString("CardFaces", value);
			Changed?.Invoke(null, EventArgs.Empty);
		}
	}
}
