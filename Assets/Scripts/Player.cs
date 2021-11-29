using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player {
    public Queue<Card> cards = new Queue<Card>(52);
    public bool isActiveInRound;
    public bool isActiveInGame = true;
	public bool isComputer = false;
    public Card activeRoundCard;
	public PlayerPanel panel;

	public bool isActive {
        get {
            return this.isActiveInGame && this.isActiveInRound;
        }
    }

	public void UpdateCardsCount() {
        this.panel.cardsLabel.text = "Cards: " + this.cards.Count;
    }
}
