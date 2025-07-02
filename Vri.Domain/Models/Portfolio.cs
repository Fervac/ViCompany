using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using static System.Formats.Asn1.AsnWriter;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Vri.Domain.Models;

public class Portfolio
{
    public Portfolio(decimal startCash, IEnumerable<Transaction> transactions)
    {
        CashPosition = startCash;
        //Start with the user's initial cash before any transactions are processed.

        var dict = new Dictionary<string, (int buyQty, decimal buyCost, int sellQty)>();
        //For each instrument(identified by ISIN), this dictionary tracks:
        //buyQty: Total quantity bought.
        //buyCost: Total cost of all buys(for weighted average price).
        //sellQty: Total quantity sold.

        foreach (var tx in transactions)
        {
            if (!dict.TryGetValue(tx.Isin, out var entry))
                entry = (0, 0m, 0);
            //For each transaction, check if we already have an entry for this ISIN.
            //If not, initialize it with zeros.

            if (tx.Type == TransactionType.Buy)
            {
                entry.buyQty += tx.Quantity;
                entry.buyCost += tx.Quantity * tx.PricePerUnit;
                CashPosition -= tx.Quantity * tx.PricePerUnit;
                //Increase the total bought quantity for this ISIN.
                //Increase the total buy cost(for weighted average).
                //Subtract the buy amount from the cash position(because buying costs money).
            }
            else if (tx.Type == TransactionType.Sell)
            {
                entry.sellQty += tx.Quantity;
                CashPosition += tx.Quantity * tx.PricePerUnit;
                //Increase the total sold quantity for this ISIN.
                //Add the sell amount to the cash position(because selling brings in money).
            }

            dict[tx.Isin] = entry;
            //Update the dictionary with the new values for this ISIN.
        }

        Instruments = dict
            .Select(kvp =>
            {
                var isin = kvp.Key;
                var (buyQty, buyCost, sellQty) = kvp.Value;
                var netQty = buyQty - sellQty;
                if (netQty <= 0) return null; // Only include positive positions

                var avgPrice = buyQty > 0 ? buyCost / buyQty : 0m;
                return new Position
                {
                    Isin = isin,
                    Quantity = netQty,
                    Price = avgPrice
                };
            })
            .Where(pos => pos != null)
            .ToList()
            .AsReadOnly();

        //For Each ISIN in the Dictionary
        //        Calculate net quantity:
        //        netQty = buyQty - sellQty;
        //        (How many units are left after all buys and sells.)

        //Skip if net quantity is zero or negative:
        //if (netQty <= 0) return null;
        //        (Don't include instruments where you have sold all or more than you bought.)

        //Calculate average buy price:
        //avgPrice = buyQty > 0 ? buyCost / buyQty : 0m;
        //        (Weighted average price, only considering buys.)

        //Create a Position instance:
        //Store ISIN, net quantity, and average buy price.

        //Filter out nulls and finalize the list
        //.Where(pos => pos != null)
        //(Remove any positions that were skipped because net quantity was zero or less.)

        //.ToList().AsReadOnly()
        //(Convert to a read - only list for safety.)
    }

    public decimal CashPosition { get; set; }

    public IReadOnlyList<Position> Instruments { get; set; } = new List<Position>(); 
}

//In Short
//Track how much was bought, how much was sold, and the total cost of buys for each instrument.
//Update the cash position as you process each transaction.
//Only keep instruments you still own (net quantity > 0).
//The average price is always based on all buys, not adjusted for sells.