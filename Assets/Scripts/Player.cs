using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player {
    public Queue<Card> cards = new Queue<Card>(52);
    public bool isActiveInRound;
    public bool isActiveInGame = true;
    public Card activeRoundCard;
	public PlayerPanel panel;

	public bool isActive {
        get {
            return this.isActiveInGame && this.isActiveInRound;
        }
    }

	public void UpdateCardsCound() {
        this.panel.cardsLabel.text = "Cards: " + this.cards.Count;
    }
}
