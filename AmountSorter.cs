using System;

using System.Collections.Generic;
namespace SortByAmountMod
{
	public class AmountSorter : ICommListener
	{

 		Dictionary<int, int> cardTypeAmount;
		protected void increaseAmount(int cardType) {
			if (cardTypeAmount.ContainsKey(cardType)) {
				cardTypeAmount [cardType] += 1;
			} else {
				cardTypeAmount [cardType] = 1;
			}
		}
		public int getAmount(Card c) {
			return getAmount (c.getCardType ().id);
		}
		public int getAmount(int cardType) {
			if (cardTypeAmount.ContainsKey(cardType)) {
				return cardTypeAmount [cardType];
			} else {
				return 0;
			}
		}
		public void handleMessage (Message msg)
		{
			if (msg is LibraryViewMessage) {
				cardTypeAmount = new Dictionary<int, int> ();
				LibraryViewMessage libraryViewMessage = (LibraryViewMessage)msg;
				foreach (Card card in libraryViewMessage.cards)
				{
					increaseAmount (card.getCardType ().id);
				}
			}
		}

		public int CompareByAmount(Card a, Card b) {
			return getAmount (a).CompareTo (getAmount (b));
		}
		public void onReconnect () {}

	}
}

