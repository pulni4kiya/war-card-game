using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player {
    public Queue<Card> cards = new Queue<Card>(52);
    public bool isActiveInRound;
    public bool isActiveInGame = true;
    public Card activeRoundCard;
    public Vector3 cardsPosition;

    public bool isActive {
        get {
            return this.isActiveInGame && this.isActiveInRound;
        }
    }
}
