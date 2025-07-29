using System.Collections.Generic;

namespace CardFool
{
    /// <summary>
    /// Колода
    /// </summary>
    public enum Suits { Hearts, Diamonds, Clubs, Spades };   // черви, бубны, крести, пики

    /// <summary>
    /// Карта
    /// </summary>
    public struct SCard
    {
        public Suits Suit { get; private set; }    // масть, от 0 до 3
        public int Rank { get; private set; }      // величина, от 6 до 14

        public SCard(Suits suit, int rank)
        {
            Suit = suit;
            Rank = rank;
        }
        const string suit = "чбкп";
        const string rank = "-123456789_ВДКТ";
        public override string ToString()
        {
            if (Rank == 0) return "--";
            
            if (Rank == 10)
                return suit[(int)Suit] + "10";

            return suit[(int)Suit].ToString() + rank[Rank];
        }
        public static bool CanBeat(SCard down, SCard up, Suits trump)
        {
            //Если карта козырь, она бьёт любую некозырную
            if (up.Suit == trump && down.Suit != trump)
                return true;

            //Иначе обычные правила:
            return down.Suit == up.Suit && down.Rank < up.Rank;
        }

    }
    /// <summary>
    /// Пара карт на столе
    /// </summary>
    public struct SCardPair
    {
        private SCard _down;    // карта снизу
        private SCard _up;      // карта сверху
        public bool Beaten { get; private set; }   // признак бита карта или нет

        //Получение или установка нижней карты
        public SCard Down
        {
            get { return _down; }
            set { _down = value; Beaten = false; _up = new SCard(); }
        }
        //Верхняя карта
        public SCard Up
        {
            get { return _up; }
        }
        //Установка верхней
        public bool SetUp(SCard up, Suits trump)
        {
            if (!SCard.CanBeat(_down, up, trump))
                return false;

            _up = up;
            Beaten = true;
            return true;
        }
        //Конструктор из нижней карты
        public SCardPair(SCard down)
        {
            _down = down;
            _up = new SCard();
            Beaten = false;
        }
        public override string ToString()
        {
            return _down.ToString() + " vs " + _up.ToString();
        }

        /// <summary>
        /// Преобразование всех карт в список пар с указанной нижней картой
        /// </summary>
        public static List<SCardPair> CardsToCardPairs(List<SCard> cards)
        { 
           return cards.ConvertAll(x => new SCardPair(x));
        }
        //Проверка: "может ли быть карта доброшена к этой паре?"
        public static bool CanBeAddedToPair(SCard newCard, SCardPair pair)
        {
            if (newCard.Rank == pair.Down.Rank)
                return true;
            return pair.Beaten && newCard.Rank == pair.Up.Rank;
        }

    }

    /// <summary>
    /// Результат пары защиты/атаки
    /// </summary>
    public enum EndRound { Continue, Take, Defend, Error };

    /// <summary>
    /// Результат игры
    /// </summary>
    public enum EndGame { Error, First, Second, Draw, Continue };

    public class MGameRules
    {

		// Количество карт на руке при раздаче
		public const int TotalCards = 6;

		//Создание всей колоды
		public static List<SCard> GetDeck()
		{
			List<SCard> temp = [];
			for (Suits Suit = 0; Suit <= Suits.Spades; Suit++)
				for (int Rank = 6; Rank <= 14; Rank++)
					temp.Add(new SCard(Suit, Rank));

			return temp;
		}
	}
}
