using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ExtensionMethods {
	public static void Shuffle<T>(this IList<T> list) {
		for (int n = list.Count - 1; n > 0; n--) {
			int k = Random.Range(0, n + 1);
			T tmp = list[k];
			list[k] = list[n];
			list[n] = tmp;
		}
	}
}
