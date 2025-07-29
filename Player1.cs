using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Reflection;

namespace CardFool
{
    public class MPlayer1
    {
        private string Name = "MPlayer1";
        private List<SCard> hand = new List<SCard>();       // карты на руке

        private static SCard TrumpCard;
        private List<SCard> DumpCards = new List<SCard>();
        private List<SCard> KnownOpponentCards = new List<SCard>();
        private bool IsMyAttack = false;
        private int OpponentCount = 6;
        private int DeckCount = 24;
        private bool IsAllOpponentCardsKnown = false;
        private SCard LastUnbeatenSingleCard = new SCard(0, 0);
        // Возвращает имя игрока
        public string GetName()
        {
            return Name;
        }
        //Возвращает количество карт на руке
        public int GetCount()
        {
            return hand.Count;
        }
        //Добавление карты в руку, во время добора из колоды, или взятия карт
        public void AddToHand(SCard card)
        {
            InsertWithPriority(hand, card);
        }

        private static void InsertWithPriority(List<SCard> cardsList, SCard card)
        {
            int c = 0;
            bool isTrump = card.Suit == TrumpCard.Suit;
            bool isCurrentTrump;
            while (c < cardsList.Count)
            {
                isCurrentTrump = cardsList[c].Suit == TrumpCard.Suit;
                if (!isTrump && isCurrentTrump)
                    break;
                else if (isTrump && !isCurrentTrump)
                    c++;
                else
                {
                    if (card.Rank < cardsList[c].Rank)
                        break;
                    c++;
                }
            }
            cardsList.Insert(c, card);
        }

        //Начальная атака
        public List<SCard> LayCards()
        {
            IsMyAttack = true;
            SCard c = GetCardToLay();
            hand.Remove(c);
            return [c];
        }

        private SCard GetCardToLay()
        {
            if (DeckCount > 0)
            {
                if (DeckCount <= 12 && LastUnbeatenSingleCard.Rank != 0)
                {
                    foreach(SCard card in hand)
                    {
                        if (card.Suit == LastUnbeatenSingleCard.Suit && card.Rank <= 10 && card.Rank > LastUnbeatenSingleCard.Rank)
                        {
                            LastUnbeatenSingleCard = card;
                            return card;
                        }
                    }
                    LastUnbeatenSingleCard = new SCard(0, 0);
                }
                return hand[0];
            }

            GetAllOpponentCards();

            bool canWin = GetWinningStrategy(hand, KnownOpponentCards, out List<SCard> unbeatableCards);
            if (canWin)
                return unbeatableCards[0];

            if (OpponentCount == 1)
            {
                if (unbeatableCards.Count == 0)
                    return hand[0];
                return unbeatableCards[0];
            }
            if (unbeatableCards.Count == 0)
            {
                Dictionary<int, int> popularRanks = new Dictionary<int, int>();
                for (int i = 0; i < hand.Count; i++)
                {
                    if (popularRanks.ContainsKey(hand[i].Rank))
                        popularRanks[hand[i].Rank]++;
                    else
                        popularRanks[hand[i].Rank] = 1;
                }
                int mostPopularRankCount = 0;
                int mostPopularRankIndex = 0;

                for (int i = 0; i < hand.Count; i++)
                {
                    if (mostPopularRankCount < popularRanks[hand[i].Rank] && hand[i].Suit != TrumpCard.Suit)
                    {
                        mostPopularRankCount = popularRanks[hand[i].Rank];
                        mostPopularRankIndex = i;
                    }
                }
                return hand[mostPopularRankIndex];
            }
            return hand[0];
        }

        private void GetAllOpponentCards()
        {
            if (IsAllOpponentCardsKnown) return;
            KnownOpponentCards.Clear();
            for (int c = 0; c <= 3; c++)
            {
                for (int d = 6; d <= 14; d++)
                {
                    SCard card = new SCard((Suits)c, d);
                    if (hand.Contains(card) || DumpCards.Contains(card))
                        continue;

                    KnownOpponentCards.Add(card);
                }
            }
            IsAllOpponentCardsKnown = true;
        }
        
        private bool GetWinningStrategy(List<SCard> myCards, List<SCard> opponentCards, out List<SCard> unbeatableCards)
        {
            unbeatableCards = new List<SCard>();
            List<SCard> beatableCards = new List<SCard>();
            foreach (SCard card in myCards)
            {
                bool canBeat = false;
                foreach (SCard oppCard in opponentCards)
                    if (SCard.CanBeat(card, oppCard, TrumpCard.Suit))
                    {
                        canBeat = true;
                        break;
                    }
                if (!canBeat)
                    unbeatableCards.Add(card);
                else
                    beatableCards.Add(card);
            }

            if (unbeatableCards.Count == 0)
                return false;
            if (myCards.Count - unbeatableCards.Count <= 1)
                return true;
            if (myCards.Count - unbeatableCards.Count <= 4)
            {
                int b = beatableCards[0].Rank;
                for (int i = 1; i < beatableCards.Count; i++)
                    if (beatableCards[i].Rank != b)
                        return false;
                return true;
            }
            return false;
        }

        //Защита от карт
        // На вход подается набор карт на столе, часть из них могут быть уже покрыты
        public bool Defend(List<SCardPair> table)
        {
            if (DeckCount < 4)
                return SmartDefend(table);
            else
                return SimpleDefend(table, hand);
        }

        private bool SmartDefend(List<SCardPair> table)
        {
            List<SCard> cardsToDefend = new List<SCard>();
            GetCardsToDefend(table, cardsToDefend);
            if (cardsToDefend.Count == 0)
                return false;

            for (int i = 0; i < table.Count; i++)
            {
                if (!table[i].Beaten)
                {
                    SCardPair c = table[i];
                    c.SetUp(cardsToDefend[0], TrumpCard.Suit);
                    table[i] = c;
                    hand.Remove(cardsToDefend[0]);
                    cardsToDefend.RemoveAt(0);
                }
            }
            return true;
        }

        private void GetCardsToDefend(List<SCardPair> table, List<SCard> cardsToDefend)
        {
            List<List<SCard>> possibleCardsToDefend = new List<List<SCard>>();
            for (int i = 0; i < table.Count; i++)
            {
                if (!table[i].Beaten)
                {
                    List<SCard> candidates = new List<SCard>();

                    foreach (SCard card in hand)
                    {
                        if (SCard.CanBeat(table[i].Down, card, TrumpCard.Suit))
                            InsertWithPriority(candidates, card);
                    }
                    if (candidates.Count == 0)
                        return;

                    possibleCardsToDefend.Add(candidates);
                }
            }

            List<SCard> tempOpponentCards = new List<SCard>();
            if (DeckCount <= 1)
                GetAllOpponentCards();

            foreach (SCard card in KnownOpponentCards)
                tempOpponentCards.Add(card);

            foreach (SCardPair pair in table)
                tempOpponentCards.Remove(pair.Down);

            for (int i = 0; i < possibleCardsToDefend.Count; i++)
            {
                SCard candidateToDefend;
                if (DeckCount > 1)
                    candidateToDefend = FirstCaseGetCandidate(table, possibleCardsToDefend[i], tempOpponentCards);
                else
                    candidateToDefend = SecondCaseGetCandidate(possibleCardsToDefend[i], tempOpponentCards);
                cardsToDefend.Add(candidateToDefend);
                for (int j = i + 1; j < possibleCardsToDefend.Count; j++)
                {
                    possibleCardsToDefend[j].Remove(candidateToDefend);
                    if (possibleCardsToDefend[j].Count == 0)
                    {
                        cardsToDefend.Clear();
                        return;
                    }
                }
            }
            if (!IsDefenceOptimal(table, cardsToDefend, tempOpponentCards))
                cardsToDefend.Clear();
        }

        private bool IsDefenceOptimal(List<SCardPair> table, List<SCard> cardsToDefend, List<SCard> opponentCards)// считая что всегда подбрасывает не козыри
        {
            List<SCard> tempHand = new List<SCard>();
            foreach (SCard card in hand)
                tempHand.Add(card);
            foreach (SCard card in cardsToDefend)
                tempHand.Remove(card);

            //список некозырных карт которые может подкинуть соперник
            List<SCard> opponentToAddNotTrump = new List<SCard>();
            foreach (SCard oppCard in opponentCards)
            {
                foreach (SCardPair pair in table)
                {
                    if (oppCard.Suit != TrumpCard.Suit && SCardPair.CanBeAddedToPair(oppCard, pair))
                    {
                        opponentToAddNotTrump.Add(oppCard);
                        break;
                    }
                }
            }
            if (opponentToAddNotTrump.Count > tempHand.Count)
                opponentToAddNotTrump.RemoveRange(tempHand.Count, opponentToAddNotTrump.Count - tempHand.Count);

            //смогу ли я их отбить?
            List<SCardPair> tempTable = new List<SCardPair>();
            foreach (SCard card in opponentToAddNotTrump)
                tempTable.Add(new SCardPair(card));

            return SimpleDefend(tempTable, tempHand);
        }

        private SCard FirstCaseGetCandidate(List<SCardPair> table, List<SCard> candidates, List<SCard> opponentCards)
        {
            Dictionary<SCard, int[]> cardStats = candidates.ToDictionary(card => card, card => new int[2]);

            for (int i = 0; i < candidates.Count; i++)
            {
                foreach (SCard card in hand)
                    if (candidates[i].Rank == card.Rank)
                            cardStats[candidates[i]][0]++;

                foreach (SCard dCard in DumpCards)
                    if (candidates[i].Rank == dCard.Rank)
                            cardStats[candidates[i]][0]++;

                if (candidates[i].Rank == TrumpCard.Rank)
                        cardStats[candidates[i]][0]++;

                foreach (SCardPair pair in table)
                {
                    if (candidates[i].Rank == pair.Down.Rank)
                            cardStats[candidates[i]][0]++;
                    if (pair.Beaten && candidates[i].Rank == pair.Up.Rank)
                            cardStats[candidates[i]][0]++;
                }
            }

            for (int i = 0; i < candidates.Count; i++)
                foreach (SCard c in opponentCards)
                    if (c.Rank == candidates[i].Rank)
                        cardStats[candidates[i]][1]++;

            int minCanAdd = 4 - cardStats[candidates[0]][0];
            int indexOfMin = 0;
            for (int i = 1; i < candidates.Count; i++)
            {
                if (4 - cardStats[candidates[i]][0] < minCanAdd)
                {
                    minCanAdd = 4 - cardStats[candidates[i]][0];
                    indexOfMin = i;
                }
                else if (4 - cardStats[candidates[i]][0] == minCanAdd
                    && cardStats[candidates[i]][1] < cardStats[candidates[indexOfMin]][1])
                {
                    minCanAdd = 4 - cardStats[candidates[i]][0];
                    indexOfMin = i;
                }
            }
            return candidates[indexOfMin];
        }

        private SCard SecondCaseGetCandidate(List<SCard> candidates, List<SCard> opponentCards)
        {
            bool isOpponentHaveCardToAdd;
            for (int i = 0; i < candidates.Count; i++)
            {
                isOpponentHaveCardToAdd = false;
                foreach (SCard c in opponentCards)
                {
                    if (c.Rank == candidates[i].Rank)
                    {
                        isOpponentHaveCardToAdd = true;
                        break;
                    }
                }
                if (!isOpponentHaveCardToAdd)
                    return candidates[i];
            }
            return candidates[0];
        }


        private bool SimpleDefend(List<SCardPair> table, List<SCard> myCards)
        {
            List<SCard> cardsToDefend = new List<SCard>();
            for (int i = 0; i < table.Count; i++)
            {
                if (!table[i].Beaten)
                {
                    List<SCard> candidates = new List<SCard>();

                    foreach (SCard card in myCards)
                    {
                        if (SCard.CanBeat(table[i].Down, card, TrumpCard.Suit))
                            candidates.Add(card);
                    }

                    if (candidates.Count == 0)
                    {
                        foreach (SCard card in cardsToDefend)
                            InsertWithPriority(myCards, card);
                        return false;
                    }

                    cardsToDefend.Add(candidates[0]);
                    myCards.Remove(candidates[0]);
                }
            }
            for (int i = 0; i < table.Count; i++)
            {
                if (!table[i].Beaten)
                {
                    SCardPair c = table[i];
                    c.SetUp(cardsToDefend[0], TrumpCard.Suit);
                    table[i] = c;
                    cardsToDefend.RemoveAt(0);
                }
            }
            return true;
        }

        //Добавление карт
        //На вход подается набор карт на столе, а также отбился ли оппонент
        public bool AddCards(List<SCardPair> table, bool OpponentDefenced)
        {
            UpdateLastUnbeatenSingleCard(table, OpponentDefenced);
            List<SCard> cardsToAdd = new List<SCard>();
            int opponentCardCount = Math.Min(MGameRules.TotalCards, 36 - DumpCards.Count - table.Count - hand.Count) - table.Count;

            foreach (SCard card in hand)
            {
                foreach (SCardPair pair in table)
                {
                    if (WillAddCard(card, pair, OpponentDefenced))
                    {
                        InsertWithPriority(cardsToAdd, card);
                        break;
                    }
                }
            }
            if (cardsToAdd.Count > opponentCardCount)
            {
                if (DeckCount <= 1 && OpponentDefenced)
                {
                    List<SCard> tempOpponentCards = new List<SCard>();
                    foreach (SCard card in KnownOpponentCards)
                        tempOpponentCards.Add(card);

                    foreach (SCardPair pair in table)
                        tempOpponentCards.Remove(pair.Up);

                    if (opponentCardCount != 0 && GetWinningStrategy(cardsToAdd, tempOpponentCards, out List<SCard> unbeatableCards))
                    {
                        bool hasUnbeat = false;
                        int indexOfUnbeat = -1;
                        for(int card = 0; card < cardsToAdd.Count; card++)
                        {
                            foreach(SCard unbeatCard in unbeatableCards)
                                if(cardsToAdd[card].Suit == unbeatCard.Suit && cardsToAdd[card].Rank == unbeatCard.Rank)
                                {
                                    hasUnbeat = true;
                                    indexOfUnbeat = card;

                                    break;
                                }
                            if (hasUnbeat)
                                break;
                        }

                        if (indexOfUnbeat != -1) 
                        {
                            if (indexOfUnbeat < opponentCardCount)
                                cardsToAdd.RemoveRange(opponentCardCount, cardsToAdd.Count - opponentCardCount);
                            else
                            {
                                SCard unbeatableCard = cardsToAdd[indexOfUnbeat];
                                cardsToAdd.RemoveRange(opponentCardCount-1, cardsToAdd.Count - opponentCardCount+1);
                                cardsToAdd.Add(unbeatableCard);
                            }

                        }
                        else
                            cardsToAdd.RemoveRange(opponentCardCount, cardsToAdd.Count - opponentCardCount);
                    }
                    else
                        cardsToAdd.RemoveRange(opponentCardCount, cardsToAdd.Count - opponentCardCount);
                }
                else
                    cardsToAdd.RemoveRange(opponentCardCount, cardsToAdd.Count - opponentCardCount);
            }
            foreach (SCard card in cardsToAdd)
                hand.Remove(card);

            table.AddRange(SCardPair.CardsToCardPairs(cardsToAdd));
            return cardsToAdd.Count > 0;
        }

        private void UpdateLastUnbeatenSingleCard(List<SCardPair> table, bool OpponentDefenced)
        {
            if (!OpponentDefenced && DeckCount > 1)
            {
                if (table.Count == 1 && table[0].Down.Suit != TrumpCard.Suit)
                    LastUnbeatenSingleCard = table[0].Down;
                else if (LastUnbeatenSingleCard.Rank == 0)
                {
                    int countOfUnbeaten = 0;
                    SCard unbeatenCard = new SCard(0, 0);
                    foreach (SCardPair pair in table)
                        if (!pair.Beaten)
                        {
                            countOfUnbeaten++;
                            unbeatenCard = pair.Down;
                            if (countOfUnbeaten > 1)
                                break;
                        }

                    if (countOfUnbeaten == 1 && unbeatenCard.Suit != TrumpCard.Suit)
                    {
                        bool hasStrongerSameSuit = false;
                        foreach (SCardPair pair in table)
                        {
                            if (pair.Beaten)
                            {
                                if (pair.Down.Suit == unbeatenCard.Suit && pair.Down.Rank > unbeatenCard.Rank)
                                {
                                    hasStrongerSameSuit = true;
                                    break;
                                }
                                if (pair.Up.Suit == unbeatenCard.Suit && pair.Up.Rank > unbeatenCard.Rank)
                                {
                                    hasStrongerSameSuit = true;
                                    break;
                                }
                            }
                        }
                        if (!hasStrongerSameSuit)
                            LastUnbeatenSingleCard = unbeatenCard;
                    }
                }
            }
        }

        private bool WillAddCard(SCard card, SCardPair pair, bool OpponentDefenced)
        {
            if ((DeckCount <= 1 && !OpponentDefenced) || DeckCount > 1)
                return card.Suit != TrumpCard.Suit && SCardPair.CanBeAddedToPair(card, pair);

            return SCardPair.CanBeAddedToPair(card, pair);
        }

        //Вызывается после основной битвы, когда известно отбился ли защищавшийся
        //На вход подается набор карт на столе, а также была ли успешной защита
        public void OnEndRound(List<SCardPair> table, bool IsDefenceSuccesful)
        {
            int tempHandCount = hand.Count;
            if (IsMyAttack)
            {
                while (DeckCount > 0 && tempHandCount < MGameRules.TotalCards)
                {
                    DeckCount--;
                    tempHandCount++;
                }
                if (IsDefenceSuccesful)
                {
                    foreach (SCardPair pair in table)
                        KnownOpponentCards.Remove(pair.Up);

                    OpponentCount -= table.Count;
                    while (DeckCount > 0 && OpponentCount < MGameRules.TotalCards)
                    {
                        DeckCount--;
                        OpponentCount++;
                    }
                }
                else
                {
                    foreach (SCardPair pair in table)
                    {
                        if (pair.Beaten && !KnownOpponentCards.Contains(pair.Up))
                            KnownOpponentCards.Add(pair.Up);

                        KnownOpponentCards.Add(pair.Down);
                    }

                    OpponentCount += table.Count;
                }
            }
            else
            {
                foreach (SCardPair pair in table)
                    KnownOpponentCards.Remove(pair.Down);

                OpponentCount -= table.Count;
                while (DeckCount > 0 && OpponentCount < MGameRules.TotalCards)
                {
                    DeckCount--;
                    OpponentCount++;
                }
                if (IsDefenceSuccesful)
                {
                    while (DeckCount > 0 && tempHandCount < MGameRules.TotalCards)
                    {
                        DeckCount--;
                        tempHandCount++;
                    }
                }
            }

            if (IsDefenceSuccesful)
            {
                IsMyAttack = !IsMyAttack;

                foreach (SCardPair pair in table)
                {
                    DumpCards.Add(pair.Up);
                    DumpCards.Add(pair.Down);
                }
            }
        }

        //Установка козыря, на вход подаётся козырь, вызывается перед первой раздачей карт
        public void SetTrump(SCard NewTrump)
        {
            TrumpCard = NewTrump;
        }
    }

}
