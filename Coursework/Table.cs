using System.Collections.Generic;
using System.Linq;
using System;

namespace CardFool
{
	internal class Program
	{
		static int Main()
		{
			//Проводим дуэль с выводом на консоль
			var Table = new MTable(new MPlayer1(), new MPlayer2());

			Console.WriteLine("Winner: " + Table.PlayGame() + " player");

			//Проводим теперь 100 игр
			MTable.WriteToConsole = false;
			
			//Словарь для записи результатов матчей
			Dictionary<EndGame, int> Results = new Dictionary<EndGame, int>(){
				{ EndGame.First, 0 },
				{ EndGame.Second, 0 },
				{ EndGame.Draw, 0 },
			};

			//Проводим матчи
			for (int i = 0; i < 100; i++)
			{
				var result = new MTable(new MPlayer1(), new MPlayer2()).PlayGame();
				Results[result]++;
			}

			//Выводим результаты
			foreach (var pair in Results)
				Console.WriteLine($"{pair.Key}: {pair.Value}");
			
			return 0;
		}
	}

	public class MTable
	{
		int RoundNum = 0;
		public static bool WriteToConsole = true;


		// Колода карт в прикупе
		protected List<SCard> deck = new List<SCard>();

		protected MPlayer1 Player1;        // игрок 1
		protected MPlayer2 Player2;        // игрок 2


		protected SCard Trump;             // козырь
		protected List<SCardPair> Table = [];   // карты на столе
		protected int DumpCount = 0;
		/// <summary>
		/// Атакует ли первый игрок <br></br>
		/// Конструкция вида  IsFirstAttacking ? Player1 : Player2 получает атакующего игрока <br></br>
		/// Конструкция вида !IsFirstAttacking ? Player1 : Player2 получает защищающегося игрока
		/// </summary>
		public bool IsFirstAttacking = true;
		public MTable(MPlayer1 NewPlayer1, MPlayer2 NewPlayer2,
			bool isFirstPlayerAttacking = true)
		{
			IsFirstAttacking = isFirstPlayerAttacking;
			Player1 = NewPlayer1;
			Player2 = NewPlayer2;
		}
		public int GetPlayerCardCount(bool FirstPlayer)
		{
			return FirstPlayer ? Player1.GetCount() : Player2.GetCount();
		}
		public void AddCardToPlayer(bool FirstPlayer, SCard Card)
		{
			if (FirstPlayer)
				Player1.AddToHand(Card);
			else
				Player2.AddToHand(Card);
		}
		/// <summary>
		/// Простейшая реализация игры, полностью проводит игру без дополнительных вмешательств
		/// </summary>
		public EndGame PlayGame()
		{

			Initialize();
			while (true)
			{
				PlayPreRound();

				EndRound RoundResult;
				do
				{
					RoundResult = PlayAttack(PlayDefence());
				}
				while (RoundResult == EndRound.Continue);

				var GameResult = PlayPostRound(RoundResult);

				if (GameResult != EndGame.Continue)
					return GameResult;
			}
		}

		/// <summary>
		/// Инициализация Колоды 
		/// </summary>
		protected virtual void InitDeck()
		{
			List<SCard> temp = MGameRules.GetDeck();

			// формирование прикупа - перемешиваем карты
			for (int c = 0, end = temp.Count; c < end; c++)
			{
				int num = Random.Shared.Next(temp.Count);
				deck.Add(temp[num]);
				temp.RemoveAt(num);
			}
		}
		public void Initialize()
		{
			InitDeck();
			// формирование козыря
			Trump = deck[0];

			Player1.SetTrump(Trump);
			Player2.SetTrump(Trump);

			// раздача карт первому и второму игроку
			AddCards();
		}
		public void PlayPreRound()
		{
			//Обозначаем роли на текущий ход
			if (WriteToConsole)
			{
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.WriteLine("Раунд: " + RoundNum++);
				Console.ForegroundColor = ConsoleColor.DarkRed;
				Console.WriteLine("Атакует: " + (IsFirstAttacking ? Player1.GetName() : Player2.GetName()));
				Console.ForegroundColor = ConsoleColor.DarkGreen;
				Console.WriteLine("Защищается: " + (IsFirstAttacking ? Player1.GetName() : Player2.GetName()));
				Console.ForegroundColor = ConsoleColor.White;
			}

			// Выкладываем все карты на стол атакующего
			Table = SCardPair.CardsToCardPairs(IsFirstAttacking ? Player1.LayCards() : Player2.LayCards());

			DrawTable("Начальная атака:");

		}
		public EndRound PlayDefence()
		{
			//Обрабатываем зашиту
			var Defenced = !IsFirstAttacking ? Player1.Defend(Table) : Player2.Defend(Table);

			DrawTable("Оборона: ");

			//И если не отбился от хотя бы одной карты, то вызываем ошибку, если заявил что отбился
			if (Defenced && Table.Any(x => !x.Beaten))
				throw new Exception();

			return Defenced ? EndRound.Continue : EndRound.Take;
		}
		public EndRound PlayAttack(EndRound DefenceResult)
		{
			//Обрабатываем атаку
			bool IsDefenced = DefenceResult != EndRound.Take;
			bool added = IsFirstAttacking ? Player1.AddCards(Table, IsDefenced) : Player2.AddCards(Table, IsDefenced);

			DrawTable("Атака: ");

			//Атакующий не может докинуть больше 6 карт
			if (Table.Count > MGameRules.TotalCards)
				throw new Exception();

			//Атакующий не может подкинуть карты, которые не может отбить обороняющийся из-за недостатка карт
			//Формально: количество небитых карт не должно превышать количество карт в руке оппонента
			if (Table.Count(x => !x.Beaten) > GetPlayerCardCount(!IsFirstAttacking))
				throw new Exception();

			if (DefenceResult == EndRound.Take)
			{
				// если не отбился, то принимает
				foreach (var pair in Table)
				{
					AddCardToPlayer(!IsFirstAttacking, pair.Down);
					if (pair.Beaten)
						AddCardToPlayer(!IsFirstAttacking, pair.Up);
				}

				return EndRound.Take;
			}

			// если отбился и подкинули, то продолжаем
			if (added)
				return EndRound.Continue;

			// если отбился, но не подкинули, то успешная защита
			return EndRound.Defend;
		}

		public EndGame PlayPostRound(EndRound RoundResult)
		{
			if (WriteToConsole)
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine("Результат раунда: " + RoundResult.ToString());
				Console.WriteLine();
				Console.ForegroundColor = ConsoleColor.White;
			}

			//Вызываем ивент конца раунда у игроков
			Player1.OnEndRound(Table.ToList(), RoundResult == EndRound.Defend);
			Player2.OnEndRound(Table.ToList(), RoundResult == EndRound.Defend);

			//Если защита была успешной, то все карты уходят в стопку сброса
			if (RoundResult == EndRound.Defend)
				DumpCount += Table.Count * 2;

			// Добавляем игрокам карты из колоды
			AddCards();

			// Если игрок защитился даём ему ход
			if (RoundResult == EndRound.Defend) 
				IsFirstAttacking = !IsFirstAttacking;

			//Собираем информацию о игроках
			int Player1Count = Player1.GetCount();
			int Player2Count = Player2.GetCount();

			//Проверяем выполнение закона сохранения карт 
			if (DumpCount + Player1Count + Player2Count + deck.Count != 36)
				throw new Exception();

			// Если конец игры, то выходим
			if (Player1Count == 0 && Player2Count == 0) return EndGame.Draw;
			if (Player1Count == 0) return EndGame.First;
			if (Player2Count == 0) return EndGame.Second;
			//А если нет то остаёмся
			return EndGame.Continue;
		}

		// Добавляем карты из колоды первому и второму игроку
		private void AddCards()
		{
			// добавляем карты атаковавшему игроку
			while (GetPlayerCardCount(IsFirstAttacking) < MGameRules.TotalCards && deck.Count > 0)
			{
				AddCardToPlayer(IsFirstAttacking, deck.Last());
				deck.RemoveAt(deck.Count - 1);
			}

			// добавляем защищавшемуся игроку
			while (GetPlayerCardCount(!IsFirstAttacking) < MGameRules.TotalCards && deck.Count > 0)
			{
				AddCardToPlayer(!IsFirstAttacking, deck.Last());
				deck.RemoveAt(deck.Count - 1);
			}
		}
		//Выводим карты на столе на консоль при этом сначала выведя Text
		private void DrawTable(string Text)
		{
			if (!WriteToConsole)
				return;
			Console.WriteLine(Text);
			Console.WriteLine(string.Join("   ", Table.ConvertAll(x => x.ToString())));
		}
	}
}
