using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ApplicationSettings {
	public static event EventHandler Changed;

	public static void RaiseChangedEvent() {
		Changed?.Invoke(null, EventArgs.Empty);
	}

	public static string CardFaces {
		get {
			return PlayerPrefs.GetString("CardFaces", "deck1");
		}
		set {
			PlayerPrefs.SetString("CardFaces", value);
		}
	}

	public static string CardBacks {
		get {
			return PlayerPrefs.GetString("CardBacks", "back1");
		}
		set {
			PlayerPrefs.SetString("CardBacks", value);
		}
	}

	public static string Background {
		get {
			return PlayerPrefs.GetString("Background", "background1");
		}
		set {
			PlayerPrefs.SetString("Background", value);
		}
	}
}
