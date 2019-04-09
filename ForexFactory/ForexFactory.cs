using System;
using NQuotes;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Newtonsoft.Json;

//

namespace MetaQuotesSample
{
    //+------------------------------------------------------------------+
    //|                                               Moving Average.mq4 |
    //|                      Copyright © 2005, MetaQuotes Software Corp. |
    //|                                       http://www.metaquotes.net/ |
    //+------------------------------------------------------------------+
    public class ForexFactory : MqlApi
    {
        [ExternVariable]
        public double lots = 0;

        [ExternVariable]
        public int message_id = 0;

        const string file_path = "C:\\Users\\damia\\AppData\\Roaming\\MetaQuotes\\Terminal\\1A412C2C36FFC677775350A51CD9EA1B\\MQL4\\Projects\\nquotes\\ForexFactory\\forwardFromMessageId.txt";
        readonly string[] pair_array = { "GOLD", "AUDUSD", "EURUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF", "USDJPY", "EURCZK", "EURDKK", "EURHKD", "EURMXN", "EURNOK", "EURPLN", "EURSEK", "EURTRY", "EURZAR", "GBPDKK", "GBPNOK", "GBPSEK", "NOKSEK", "USDCNH", "USDCZK", "USDDKK", "USDHKD", "USDHUF", "USDILS", "USDMXN", "USDNOK", "USDPLN", "USDRUB", "USDSEK", "USDSGD", "USDTRY", "USDZAR", "AUDCAD", "AUDCHF", "AUDJPY", "AUDNZD", "CADCHF", "CADJPY", "CHFJPY", "EURAUD", "EURCAD", "EURCHF", "EURGBP", "EURJPY", "EURNZD", "GBPAUD", "GBPCAD", "GBPCHF", "GBPJPY", "GBPNZD", "NZDCAD", "NZDCHF", "NZDJPY", "COPPER", "XAGUSD", "XAUEUR", "XAUUSD", "XPDUSD", "XPTUSD", "NGAS", "UKOIL", "USOIL", "AUS200", "ESP35", "EUSTX50", "FRA40", "GER30", "HKG50", "JPN225", "NAS100", "SPX500", "UK100", "US30" };
        readonly char[] separator = { ' ', '\n' };
        string pair;
        int ticket, order_operation;
        double stop_loss, take_profit;
        bool restart;
        List<int> ticket_list;
        List<double> forward_from_message_id, trade_position;
        List<string> words;
        List<Message> messages;

        public ForexFactory()
        {
            this.pair = "";
            this.ticket = 0;
            this.order_operation = -1;
            this.stop_loss = 0;
            this.take_profit = 0;
            this.restart = true;
            this.ticket_list = new List<int>();
            this.forward_from_message_id = new List<double>();
            this.trade_position = new List<double>();
            this.words = new List<string>();
            this.messages = new List<Message>();
        }

        /// <summary>
        /// Main function, getting all new message 
        /// </summary>
        /// <returns></returns>
        public override int start()
        {
            try
            {
                using (var client = new WebClient())
                {
                    if (this.restart)
                    {
                        ReadTxt();
                        this.restart = false;
                    }

                    AddTicketNumber();
                    string responseString = client.DownloadString("https://api.telegram.org/bot785759223:AAEcNhHrPxYskPmsmmfrvJgtX4MTgp16loQ/getUpdates");
                    JObject json = JObject.Parse(responseString);

                    for (int i = 0; i < ((JArray)json["result"]).Count; i++)
                        this.messages.Add(JsonConvert.DeserializeObject<Message>((json["result"][i]["message"]).ToString()));

                    for (int i = 0; i < this.messages.Count; i++)
                    {
                        if (message_id < this.messages[i].message_id)
                        {
                            int extra = 0;
                            bool reply = false;

                            for (int j = 0; j < this.forward_from_message_id.Count; j++)
                            {
                                if (this.forward_from_message_id[j] == this.messages[i].forward_from_message_id)
                                {
                                    reply = true;
                                    this.messages[i].text += " reply";
                                    break;
                                }
                            }

                            if (i + 1 < ((JArray)json["result"]).Count && reply)
                            {                     
                                for (int j = i + 1; j < ((JArray)json["result"]).Count - 1; j++)
                                {
                                    if (this.messages[i].text == this.messages[j].text)
                                        extra++;

                                    else
                                        break;
                                }                               
                            }

                            message_id = this.messages[i].message_id;
                            if (this.messages[i].text != null)
                            {
                                TypeOfMessage(this.messages[i].text + " " + this.messages[i].forward_from_message_id.ToString());
                                i += extra;
                            }
                        }
                    }
                    this.messages.Clear();
                    CheckingForCloseOrders();
                    Thread.Sleep(30000);
                }
            }

            catch (Exception e)
            {
                TextForRevision(e.ToString());
            }

            return 0;
        }

        /// <summary>
        /// Verify the type of message: new trade, close trade or modify trade
        /// </summary>
        /// <param name="txt"></param>
        void TypeOfMessage(string txt)
        {
            string[] arr = txt.Split(separator);

            for (int i = 0; i < arr.Length - 1; i++)
            {
                // First IF STATEMENT for executeTrade method
                if (arr[i].ToLower() == "sell" && arr[i + 1].ToLower() == "now" || arr[i].ToLower() == "buy" && arr[i + 1].ToLower() == "now" || arr[i].ToLower() == "copyrights©️reserved")
                {
                    bool complete_order = true;

                    for (int j = 0; j < arr.Length; j++)
                    {
                        if (arr[j].ToLower() == "details" || arr[j].ToLower() == "coming")
                        {
                            complete_order = false;
                            break;
                        }
                    }

                    if (complete_order)
                        ExecuteTrade(txt);

                    break;
                }

                //Second IF STATEMENT for closeTrade method
                if (arr[i].ToLower() == "close" || arr[i].ToLower() == "half" || arr[i].ToLower() == "delete")
                {
                    bool close_full_trade = true;

                    for (int j = 0; j < arr.Length; j++)
                    {
                        if (arr[j].ToLower() == "half" || arr[j].ToLower() == "break-even♻️" || arr[j].ToLower() == "breakeven♻️" || arr[j].ToLower() == "break-even" || arr[j].ToLower() == "breakeven")
                        {
                            close_full_trade = false;
                            break;
                        }

                        if (arr[j].ToLower() == "all")
                            CloseAllTrades();
                    }

                    if (this.ticket > 0)
                        CloseTrade(txt, close_full_trade);

                    this.ticket = 0;
                    break;
                }

                //Third IF STATEMENT for modify method
                if (arr[i].ToLower() == "modify" || arr[i].ToLower() == "move")
                {
                    for (int j = 0; j < arr.Length; j++)
                    {
                        if (arr[j].ToLower() == "break-even" || arr[j].ToLower() == "breakeven" || arr[j].ToLower() == "break-even♻️" || arr[j].ToLower() == "breakeven♻️")
                        {
                            OrderSelect(this.ticket, SELECT_BY_TICKET);
                            txt = Convert.ToString(OrderOpenPrice());
                            break;
                        }
                    }

                    if (this.ticket > 0)
                        Modify(txt);

                    this.ticket = 0;
                    break;
                }

                //FOUR IF STATEMENT TO REMOVE STOPLOSS 
                if (arr[i].ToLower() == "remove")
                {
                    if (this.ticket > 0)
                        RemoveStopLoss();

                    this.ticket = 0;
                    break;
                }

                // Make ticket equal 0
                if (ticket > 0 && arr[i].ToLower() == "hit" || arr[i].ToLower() == "unfortunately")
                {
                    ticket = 0;
                    break;
                }
            }

        }

        /// <summary>
        /// Get all the information of the trade and execute it
        /// </summary>
        /// <param name="txt"></param>
        void ExecuteTrade(string txt)
        {
            string[] numbers = Regex.Split(txt, @"[^0-9\.]+");
            string[] arr = txt.Split(separator);

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != "")
                    this.words.Add(arr[i].ToUpper());
            }

            this.pair = GetPair();
            this.order_operation = GetOrderType();
            GetTradePositions(numbers);
            PendingOrder();

            // PLACE NEW ORDER  
            if (this.words[this.words.Count - 2] != "REPLY")
            {
                if (this.order_operation == 0 || this.order_operation == 2 || this.order_operation == 4)
                    BuyPositions();

                if (this.order_operation == 1 || this.order_operation == 3 || this.order_operation == 5)
                    SellPositions();

                if (this.ticket > 0)
                    OrderRepeated();

                PlaceOrder(txt);
            }

            //GET TICKET NUMBER IF THE ORDER HAS BEEN ALREADY PLACED IT 
            else
            {
                for (int i = 0; i < this.forward_from_message_id.Count; i++)
                {
                    if (this.forward_from_message_id[i] == this.trade_position[this.trade_position.Count - 1])
                    {
                        this.ticket = this.ticket_list[i];
                        break;
                    }
                }
            }

            this.words.Clear();
            this.trade_position.Clear();
            this.pair = "";
            this.order_operation = -1;
            this.stop_loss = 0;
            this.take_profit = 0;
        }

        /// <summary>
        /// Gets the pair of a trade
        /// </summary>
        /// <returns></returns>
        string GetPair()
        {
            //GET THE PAIR OF THE ORDER THAT WAS SENT

            int pos = 0;
            int amount = 0;
            int aux = 0;

            for (int i = 0; i < pair_array.Length; i++)
            {
                for (int j = 0; j < this.words.Count; j++)
                {
                    if (pair_array[i].Length == this.words[j].Length)
                    {
                        for (int k = 0; k < this.words[j].Length; k++)
                        {
                            if (pair_array[i][k] == this.words[j][k])
                            {
                                aux++;
                            }
                        }

                        if (aux > amount)
                        {
                            amount = aux;
                            pos = i;

                        }

                        aux = 0;
                    }
                }
            }

            if (pair_array[pos] == "GOLD")
                return "XAUUSD";

            return pair_array[pos];
        }

        /// <summary>
        /// Gets the order type of a trade <Sell> or <Buy>
        /// </summary>
        /// <returns></returns>
        int GetOrderType()
        {
            //DETERMINE IF THE ORDER IS A BUY OR A SELL

            for (int i = 0; i < this.words.Count; i++)
            {
                if (this.words[i].ToLower() == "buy")
                    return 0;

                if (this.words[i].ToLower() == "sell")
                    return 1;
            }

            return -1;
        }

        /// <summary>
        /// Gets the positions of a trade from an array of string and parse to a list of double
        /// </summary>
        /// <param name="numbers"></param>
        void GetTradePositions(string[] numbers)
        {
            for (int i = 0; i < numbers.Length; i++)
            {
                try
                {
                    if (numbers[i].Length > 3)
                        this.trade_position.Add(Convert.ToDouble(numbers[i]));
                }

                catch
                {
                    if (this.trade_position.Count == 0)
                    {
                        if (this.order_operation == 0)
                            this.trade_position.Add(MarketInfo(this.pair, MODE_ASK));

                        if (this.order_operation == 1)
                            this.trade_position.Add(MarketInfo(this.pair, MODE_BID));
                    }

                    TextForRevision("Exception in <GET TRADE POSITIONS >");
                    continue;
                }
            }
        }

        /// <summary>
        /// DETERMINE IF THE STOP LOSS AND THE TAKE PROFIT ARE CORRECT FOR A BUY ORDER
        /// </summary>
        void BuyPositions()
        {
            if (this.order_operation < 2)
                this.trade_position[0] = MarketInfo(this.pair, MODE_ASK);

            if (this.trade_position.Count == 2)
            {
                if (this.trade_position[0] < this.trade_position[1])
                {
                    this.stop_loss = 0;
                    this.take_profit = this.trade_position[1];
                }

                if (this.trade_position[0] > this.trade_position[1])
                {
                    this.stop_loss = this.trade_position[1];
                    this.take_profit = 0;
                }
            }

            if (this.trade_position.Count >= 3)
            {
                if (this.trade_position[0] > this.trade_position[1])
                    this.stop_loss = this.trade_position[1];

                if (this.trade_position[0] < this.trade_position[2])
                    this.take_profit = this.trade_position[2];
            }

            // IF THERE IS NOT STOP LOSS IT WOULD CREATE ONE OF 45 PIPS FOR A BUY FUNCTION

            if (this.stop_loss == 0)
            {
                int vdigits = (int)MarketInfo(this.pair, MODE_DIGITS);
                if (vdigits == 2)
                    this.stop_loss = Math.Abs(this.trade_position[0] - (45 * 0.1));

                else if (vdigits == 3)
                    this.stop_loss = Math.Abs(this.trade_position[0] - (45 * 0.01));

                else if (vdigits == 4)
                    this.stop_loss = Math.Abs(this.trade_position[0] - (45 * 0.001));

                else
                    this.stop_loss = Math.Abs(this.trade_position[0] - (45 * 0.0001));

                TextForRevision("Stop loss added manually");
            }

        }

        /// <summary>
        /// DETERMINE IF THE STOP LOSS AND THE TAKE PROFIT ARE CORRECT FOR A SELL ORDER
        /// </summary>
        void SellPositions()
        {
            if (this.order_operation < 2)
                this.trade_position[0] = MarketInfo(this.pair, MODE_BID); ;

            if (this.trade_position.Count == 2)
            {
                if (this.trade_position[0] > this.trade_position[1])
                {
                    this.stop_loss = 0;
                    this.take_profit = this.trade_position[1];
                }

                if (this.trade_position[0] < this.trade_position[1])
                {
                    this.stop_loss = this.trade_position[1];
                    this.take_profit = 0;
                }
            }

            if (this.trade_position.Count >= 3)
            {
                if (this.trade_position[0] < this.trade_position[1])
                    this.stop_loss = this.trade_position[1];

                if (this.trade_position[0] > this.trade_position[2])
                    this.take_profit = this.trade_position[2];
            }

            // IF THERE IS NOT STOP LOSS IT WOULD CREATE ONE OF 45 PIPS FOR A SELL FUNCTION

            if (this.stop_loss == 0)
            {
                int vdigits = (int)MarketInfo(this.pair, MODE_DIGITS);
                if (vdigits == 2)
                    this.stop_loss = Math.Abs(this.trade_position[0] + (45 * 0.1));

                else if (vdigits == 3)
                    this.stop_loss = Math.Abs(this.trade_position[0] + (45 * 0.01));

                else if (vdigits == 3)
                    this.stop_loss = Math.Abs(this.trade_position[0] + (45 * 0.001));

                else
                    this.stop_loss = Math.Abs(this.trade_position[0] + (45 * 0.0001));

                TextForRevision("Stop loss added manually");
            }
        }

        /// <summary>
        /// iN CASE THE AN ORDER IS REPEATED AND ALL THE PARAMETERS ARE NOT SENT FOR THE <OrderSend> FUNCTION
        /// </summary>
        void OrderRepeated()
        {
            OrderSelect(this.ticket, SELECT_BY_TICKET);
            if (OrderSymbol() == this.pair)
            {
                if (this.pair == "")
                    this.pair = OrderSymbol();

                if (this.order_operation == -1)
                    this.order_operation = OrderType();

                if (this.stop_loss == 0)
                    this.stop_loss = OrderStopLoss();

                if (this.take_profit == 0)
                    this.take_profit = OrderTakeProfit();
            }

            this.ticket = 0;
        }

        /// <summary>
        /// Verify if the trade is a Pending Order
        /// </summary>
        void PendingOrder()
        {
            for (int i = 0; i < this.words.Count; i++)
            {
                if (this.words[i].ToLower() == "limit")
                {
                    this.order_operation += 2;
                    break;
                }

                if (this.words[i].ToLower() == "stop")
                {
                    this.order_operation += 4;
                    break;
                }
            }
        }

        /// <summary>
        /// Open the trade and get the ticket number
        /// </summary>
        /// <param name="txt"></param>
        void PlaceOrder(string txt)
        {
            int n = 0;

            if (this.order_operation == 0 && this.pair != "")
                n = OrderSend(this.pair, OP_BUY, lots, Ask, 100, this.stop_loss, this.take_profit);

            if (this.order_operation == 1 && this.pair != "")
                n = OrderSend(this.pair, OP_SELL, lots, Bid, 100, this.stop_loss, this.take_profit);

            if (this.order_operation == 2 && this.pair != "")
                n = OrderSend(this.pair, OP_BUYLIMIT, lots, this.trade_position[0], 100, this.stop_loss, this.take_profit);

            if (this.order_operation == 3 && this.pair != "")
                n = OrderSend(this.pair, OP_SELLLIMIT, lots, this.trade_position[0], 100, this.stop_loss, this.take_profit);

            if (this.order_operation == 4 && this.pair != "")
                n = OrderSend(this.pair, OP_BUYSTOP, lots, this.trade_position[0], 100, this.stop_loss, this.take_profit);

            if (this.order_operation == 5 && this.pair != "")
                n = OrderSend(this.pair, OP_SELLSTOP, lots, this.trade_position[0], 100, this.stop_loss, this.take_profit);

            if (n > 0)
            {
                this.ticket_list.Add(n);
                this.forward_from_message_id.Add(this.trade_position[this.trade_position.Count - 1]);
                WriteTxt();
            }

            // IF THE TRADE WAS NOT OPENED
            if (n == -1)
                TextForRevision(txt + " << THE ORDER DID NOT EXECUTE >>");

            if (this.order_operation < 0 || this.order_operation > 5 || this.pair == "")
                TextForRevision(txt += " << IT DID NOT GET THE ORDER DETAILS >>");
        }

        /// <summary>
        /// Close trade selected by the trader
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="close_full_trade"></param>
        void CloseTrade(string txt, bool close_full_trade)
        {
            // if close_full_trade is true <close the full trade> 
            // if close_full_trade is false <close half trade and put stop loss to break even>

            bool closing_order = true;
            bool modify_order = true;
            OrderSelect(this.ticket, SELECT_BY_TICKET);

            if (OrderType() <= 1 || OrderProfit() != 0)
            {
                double LOT = 0;
                OrderSelect(this.ticket, SELECT_BY_TICKET);
                double order_lots = OrderLots();

                int pos = this.ticket_list.IndexOf(this.ticket);
                this.ticket_list.RemoveAt(pos);

                if (close_full_trade)
                {
                    closing_order = OrderClose(this.ticket, OrderLots(), Ask, 100);
                    this.forward_from_message_id.RemoveAt(pos);
                    WriteTxt();
                }

                else
                {
                    LOT = Math.Round(OrderLots() / 2, 2);
                    if (OrderLots() - LOT > LOT)
                        LOT = Math.Round(OrderLots() - LOT, 2);

                    System.DateTime expiration = new DateTime();
                    if (OrderStopLoss() != OrderOpenPrice())
                        modify_order = OrderModify(this.ticket, OrderOpenPrice(), OrderOpenPrice(), OrderTakeProfit(), expiration);

                    closing_order = OrderClose(this.ticket, LOT, Ask, 100);
                    AddTicketNumber();

                    this.forward_from_message_id.Add(this.forward_from_message_id[pos]);
                    this.forward_from_message_id.RemoveAt(pos);
                    WriteTxt();
                }
            }

            else
                closing_order = OrderDelete(this.ticket);

            if (!closing_order || !modify_order)
                TextForRevision(txt += " <<< CLOSETRADE FUNCTION >>>");

        }

        /// <summary>
        /// Modify the stop loss or take profit of a trade
        /// </summary>
        /// <param name="txt"></param>
        void Modify(string txt)
        {
            //MODIFY THE STOP LOSS OR TAKE PROFIT

            OrderSelect(this.ticket, SELECT_BY_TICKET);
            bool order_modify = true;
            double n = -1;
            this.stop_loss = OrderStopLoss();
            this.take_profit = OrderTakeProfit();
            string[] numbers = Regex.Split(txt, @"[^0-9\.]+");
            string[] arr = txt.Split(separator);

            for (int i = 0; i < numbers.Length; i++)
            {
                if (numbers[i] != "")
                {
                    n = double.Parse(numbers[i]);
                    break;
                }
            }

            OrderSelect(this.ticket, SELECT_BY_TICKET);
            if (n == -1)
                n = OrderOpenPrice();

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].ToLower() == "tp" || arr[i].ToLower() == "tp:")
                {
                    this.take_profit = n;
                    break;
                }

                if (arr[i].ToLower() == "sl" || arr[i].ToLower() == "sl:")
                {
                    this.stop_loss = n;
                    break;
                }
            }

            if (OrderType() == 0)
            {
                if (this.take_profit < OrderOpenPrice())
                    this.take_profit = 0;

                if (this.stop_loss > OrderOpenPrice())
                    this.stop_loss = 0;
            }

            if (OrderType() == 1)
            {
                if (this.take_profit > OrderOpenPrice())
                    this.take_profit = 0;

                if (this.stop_loss < OrderOpenPrice())
                    this.stop_loss = 0;
            }

            if (this.stop_loss == 0 || this.take_profit == 0)
                TextForRevision("modifcation done on TakeProfit or StopLoss with cero as a value");

            System.DateTime expiration = new DateTime();
            order_modify = OrderModify(this.ticket, OrderOpenPrice(), this.stop_loss, this.take_profit, expiration);
            if (!order_modify)
                TextForRevision(txt += " <<< Modify Stop Loss or Take Profit >>>");

        }

        /// <summary>
        /// Checks for any new close order
        /// </summary>
        void CheckingForCloseOrders()
        {
            //CHECK FOR THE ORDERS CLOSED AND REMOVE THEIR TICKETS FROM THE LIST

            for (int i = 0; i < this.ticket_list.Count; i++)
            {
                OrderSelect(this.ticket_list[i], SELECT_BY_TICKET);

                // If the value that OrderCloseTime() return is different to <<<1/1/0001 12:00:00 AM>>> the trade is closed

                if (OrderCloseTime() != Convert.ToDateTime("1/1/0001 12:00:00 AM"))
                {
                    this.ticket_list.RemoveAt(i);
                    this.forward_from_message_id.RemoveAt(i);
                }
            }
            WriteTxt();
            this.ticket = 0;
        }

        /// <summary>
        /// Send alert text to my Telegram Chanel
        /// </summary>
        /// <param name="txt"></param>
        void TextForRevision(string txt)
        {
            //SEND A TELEGRAM MESSAGE IN CASE SOMETHING WENT WRONG

            string apiToken = "";
            string chatId = "";
            string urlString = "https://api.telegram.org/bot{0}/sendMessage?chat_id={1}&text={2}";

            apiToken = "714493268:AAEIGn-fxh8L_yrEtryy0G2yvXaOOPDDTWU";
            chatId = "474653809";

            txt += " Forex Factory Paid Group";

            urlString = String.Format(urlString, apiToken, chatId, txt);

            WebRequest request = WebRequest.Create(urlString);

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                Stream rs = response.GetResponseStream();
            }
        }

        /// <summary>
        /// Add the ticket number of each trade to ticket_list
        /// </summary>
        void AddTicketNumber()
        {
            //ADD THE TICKET NUMBERS OF THE ORDERS TO <ticket_list>

            int puntero = 0;
            while (true)
            {
                bool ticket_in_list = false;
                OrderSelect(puntero, SELECT_BY_POS);
                if (OrderTicket() != 0)
                {
                    for (int i = 0; i < this.ticket_list.Count; i++)
                    {
                        if (OrderTicket() == this.ticket_list[i])
                        {
                            ticket_in_list = true;
                            break;
                        }
                    }

                    if (!ticket_in_list)
                    {
                        this.ticket_list.Add(OrderTicket());
                    }

                    puntero++;
                }

                else
                {
                    this.ticket_list.Sort();
                    break;
                }
            }
        }

        /// <summary>
        /// Close all trades
        /// </summary>
        void CloseAllTrades()
        {
            // CLOSE ALL OPEN TRADES

            for (int i = 0; i < this.ticket_list.Count; i++)
            {
                OrderSelect(this.ticket_list[i], SELECT_BY_TICKET);
                OrderClose(this.ticket_list[i], OrderLots(), Ask, 100);
            }

            this.ticket_list.Clear();
            this.forward_from_message_id.Clear();
            WriteTxt();
        }

        /// <summary>
        /// Remove the stop loss of a selected order
        /// </summary>
        void RemoveStopLoss()
        {
            // REMOVE A STOP LOSS OF A SELECTED ORDER

            System.DateTime expiration = new DateTime();
            OrderSelect(this.ticket, SELECT_BY_TICKET);
            OrderModify(this.ticket, OrderOpenPrice(), 0, OrderTakeProfit(), expiration);
        }

        /// <summary>
        /// Write the data from forward_from_message_id to forwardFromMessageId.txt
        /// </summary>
        void WriteTxt()
        {
            // WRITE THE <forward_from_message_id> OF A MESSAGE TO A txt file

            List<string> lines = new List<string>();
            for (int i = 0; i < this.forward_from_message_id.Count; i++)
                lines.Add(this.forward_from_message_id[i].ToString());

            File.WriteAllLines(file_path, lines);
        }

        /// <summary>
        /// Read the data from forwardFromMessageId.txt and add it to forward_from_message_id
        /// </summary>
        void ReadTxt()
        {
            // READS FROM A txt file

            List<string> lines = File.ReadAllLines(file_path).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                if(lines[i] != "")
                    this.forward_from_message_id.Add(double.Parse(lines[i]));
            }
        }
    }
}
