using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameState {
    public List<Player> players = new List<Player>();
    public List<Card> currentRoundCards = new List<Card>();
    public List<Player> currentRoundWinners = new List<Player>();
    public int currentRoundCardsOffset = 0;
    public Player gameWinner;
}