using System;

using ScrollsModLoader.Interfaces;
using UnityEngine;
using Mono.Cecil;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using System.Text.RegularExpressions;

namespace SortByAmountMod
{
	public class SortByAmountMod : ScrollsModLoader.Interfaces.BaseMod
	{
		int myFilterNumber = -1;
		AmountSorter sorter = new AmountSorter ();
		Type filterType;
		FieldInfo collectionSorterField;
		FieldInfo currentSortModeField;
		FieldInfo buttonGroupItemsField;
		FieldInfo cardFilterFilters;
		Dictionary<string, MethodInfo> deckSorterMethods = new Dictionary<string, MethodInfo> ();
		private List<string> requiredMethods = new List<string>(new string[]{"clear", "byName", "byLevel", "byType", "byResourceCount"});
		public SortByAmountMod ()
		{
			collectionSorterField = typeof(DeckBuilder2).GetField ("collectionSorter", BindingFlags.Instance | BindingFlags.NonPublic);
			currentSortModeField = typeof(DeckBuilder2).GetField ("currentSortMode", BindingFlags.Instance | BindingFlags.NonPublic);
			buttonGroupItemsField = typeof(ButtonGroup).GetField ("items", BindingFlags.Instance | BindingFlags.NonPublic);

			cardFilterFilters = typeof(CardFilter).GetField ("filters", BindingFlags.Instance | BindingFlags.NonPublic);
			filterType = typeof(DeckBuilder2).Assembly.GetType ("CardFilter+Filter");

			Type t = typeof(DeckBuilder2).Assembly.GetType ("DeckSorter");
			//Just get all the Methods.
			MethodInfo[] methods = t.GetMethods (BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
			foreach (MethodInfo m in methods) {
				if (!deckSorterMethods.ContainsKey (m.Name)) {
					deckSorterMethods.Add (m.Name, m);
				} else {
					Console.WriteLine ("Ignoring " + m + "in favor of " + deckSorterMethods [m.Name]);
				}
			}
			//This fails because == does not exist/fails for FieldInfo - WTF?!
			/*if (collectionSorterField == null || currentSortModeField == null || buttonGroupItemsField == null) {
				Console.WriteLine ("Could not get all required fields");
				throw new Exception ("Could not find fields - propably an version-conflict");
			}*/
			foreach (string s in requiredMethods) {
				if (!deckSorterMethods.ContainsKey (s)) {
					Console.WriteLine("Can not find method:" + s);
					throw new Exception("Cound not find all required methods");
				}
			}

			App.Communicator.addListener (sorter);
		}

		~SortByAmountMod () {
			App.Communicator.removeListener (sorter);
		}

		public static string GetName ()
		{
			return "SortByAmount";
		}

		public static int GetVersion ()
		{
			return 2;
		}

		public static MethodDefinition[] GetHooks (TypeDefinitionCollection scrollsTypes, int version)
		{
			MethodDefinition[] method;
			method = scrollsTypes ["DeckBuilder2"].Methods.GetMethod ("Start");
			MethodDefinition dbStart, dbSetSortMode, cardFilterInit;
			if (method.Length == 1) {
				dbStart = method [0];
			} else {
				return new MethodDefinition[] { };
			}

			method = scrollsTypes ["DeckBuilder2"].Methods.GetMethod ("setSortMode");
			if (method.Length == 1) {
				dbSetSortMode = method [0];
			} else {
				return new MethodDefinition[] { };
			}

			method = scrollsTypes ["CardFilter"].Methods.GetMethod ("_init");
			if (method.Length == 1) {
				cardFilterInit = method [0];
			} else {
				return new MethodDefinition[] { };
			}
			return new MethodDefinition[] {dbStart, dbSetSortMode, cardFilterInit};
		}

		public override void BeforeInvoke (InvocationInfo info)
		{
			return;
		}

		public override void AfterInvoke (InvocationInfo info, ref object returnValue)
		{
			if (info.targetMethod == "Start") {
				if (info.target is DeckBuilder2) {
					ButtonGroup sortGroup = (ButtonGroup)typeof(DeckBuilder2).GetField ("sortGroup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue (info.target);
					IList items = (IList)buttonGroupItemsField.GetValue (sortGroup);
					myFilterNumber = items.Count;
					sortGroup.addItem ("Amount", false);
				}
			} else if (info.targetMethod == "_init") {
				if (info.arguments[0] is string[]) {
					string[] strings = (string[])info.arguments [0];
					int filterIndex = 0;
					IList filters = (IList)cardFilterFilters.GetValue (info.target);

					foreach(string s in strings) {
						string filter = s.Trim ().ToLower ();
						if (filter.Length > 0) {
							if (filter.StartsWith ("a:")) {
								MyFilter f;
								string argument = s.Substring (2);
								if (argument.Length > 0) {
									Match m = Regex.Match (argument, "^(\\d+)([\\+\\-]?)$");
									if (m.Success) {
										int amount = int.Parse (m.Groups [1].Value);

										switch (m.Groups [2].Value) {
										case "+":
											f = (Card c) => (sorter.getAmount (c) >= amount);
											break;
										case "-":
											f = (Card c) => (sorter.getAmount (c) <= amount);
											break;
										default:
											f = (Card c) => (sorter.getAmount (c) == amount);
											break;
										}
									} else {
										//Can not parse the argument => show nothing.
										f = (Card c) => (false);
									}
								} else {
									//argument not yet specified => show everything
									f = (Card c) => (true);
								}
								//Replace the "text-match"-filter with our filter.
								filters.RemoveAt (filterIndex);
								filters.Insert (filterIndex, Cast (f, filterType));
							}
							filterIndex += 1;
						}
					}
				}
			}
			return;
		}

		//Method to cast a delegate - from http://code.logos.com/blog/2008/07/casting_delegates.html
		public static Delegate Cast(Delegate source, Type type)
		{
			if (source == null)
				return null;

			Delegate[] delegates = source.GetInvocationList();
			if (delegates.Length == 1)
				return Delegate.CreateDelegate(type,
				                               delegates[0].Target, delegates[0].Method);

			Delegate[] delegatesDest = new Delegate[delegates.Length];
			for (int nDelegate = 0; nDelegate < delegates.Length; nDelegate++)
				delegatesDest[nDelegate] = Delegate.CreateDelegate(type,
				                                                   delegates[nDelegate].Target, delegates[nDelegate].Method);
			return Delegate.Combine(delegatesDest);
		}
		private delegate bool MyFilter(Card c);

		public override void ReplaceMethod (InvocationInfo info, out object returnValue)
		{
			if ((int)currentSortModeField.GetValue (info.target) == myFilterNumber) {
				returnValue = null;
				return;
			}
			currentSortModeField.SetValue (info.target, myFilterNumber);
			object collectionSorter = collectionSorterField.GetValue (info.target);
			deckSorterMethods["clear"].Invoke (collectionSorter, new object[] { });
			deckSorterMethods["addSorter"].Invoke (collectionSorter, new object[] { (Comparison<Card>)sorter.CompareByAmount });
			deckSorterMethods["byColor"].Invoke (collectionSorter, new object[] { });
			deckSorterMethods["byResourceCount"].Invoke (collectionSorter, new object[] { });
			deckSorterMethods["byName"].Invoke (collectionSorter, new object[] { });
			deckSorterMethods["byLevel"].Invoke (collectionSorter, new object[] { });
			returnValue = null;
		}

		public override bool WantsToReplace (InvocationInfo info)
		{
			return info.targetMethod == "setSortMode" && ((int)info.arguments [0]).Equals (myFilterNumber);
		}
	}
}

