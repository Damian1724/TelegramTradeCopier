using System;
using Color = System.Drawing.Color;
using NQuotes;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using IronOcr;
using System.Linq;
using Newtonsoft.Json;

namespace MetaQuotesSample
{
    //+------------------------------------------------------------------+
    //|                                               Moving Average.mq4 |
    //|                      Copyright © 2005, MetaQuotes Software Corp. |
    //|                                       http://www.metaquotes.net/ |
    //+------------------------------------------------------------------+
    public class M15 : MqlApi
    {

        [ExternVariable]
        public double LOTS = 0;

        [ExternVariable]
        public int MESSAGE_ID = 0;

        [ExternVariable]
        public int FORWARD_FROM_MESSAGE_ID = 0;

        readonly AutoOcr OCR = new AutoOcr() { ReadBarCodes = false };
        readonly System.DateTime expiration = new DateTime();
        readonly char[] separator = { ' ', '\n' };
        readonly double[] lots_percent = { 0.70, 0.20, 0.10 };
        readonly string[] pair_array = { "GOLD", "AUDUSD", "EURUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF", "USDJPY", "EURCZK", "EURDKK", "EURHKD", "EURMXN", "EURNOK", "EURPLN", "EURSEK", "EURTRY", "EURZAR", "GBPDKK", "GBPNOK", "GBPSEK", "NOKSEK", "USDCNH", "USDCZK", "USDDKK", "USDHKD", "USDHUF", "USDILS", "USDMXN", "USDNOK", "USDPLN", "USDRUB", "USDSEK", "USDSGD", "USDTRY", "USDZAR", "AUDCAD", "AUDCHF", "AUDJPY", "AUDNZD", "CADCHF", "CADJPY", "CHFJPY", "EURAUD", "EURCAD", "EURCHF", "EURGBP", "EURJPY", "EURNZD", "GBPAUD", "GBPCAD", "GBPCHF", "GBPJPY", "GBPNZD", "NZDCAD", "NZDCHF", "NZDJPY", "COPPER", "XAGUSD", "XAUEUR", "XAUUSD", "XPDUSD", "XPTUSD", "NGAS", "UKOIL", "USOIL", "AUS200", "ESP35", "EUSTX50", "FRA40", "GER30", "HKG50", "JPN225", "NAS100", "SPX500", "UK100", "US30" };
        const string file_path = @"C:\\Users\\damia\\AppData\\Roaming\\MetaQuotes\\Terminal\\9D990316CA8990E1391C63EDC022B6A3\\MQL4\\Projects\\nquotes\\M15\\forwardFromMessageId.txt";
        List<int> ticket_list, replay_id_position;
        List<double> forward_from_message_id_list, trade_position;
        string pair,txt;
        int order_operation,photo_number;
        bool restart;
        List<Message> messages;

        M15()
        {
            this.ticket_list = new List<int>();
            this.forward_from_message_id_list = new List<double>();
            this.trade_position = new List<double>();
            this.replay_id_position = new List<int>();
            this.pair = "";
            this.txt = "";
            this.order_operation = -1;
            this.photo_number = 1;
            this.restart = true;
            this.messages = new List<Message>();
        }

        /// <summary>
        /// Get all the new trades and verify the type of them
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
                        DeletePictures();
                        this.restart = false;
                    }

                    AddTicketNumber();
                    string responseString = client.DownloadString("https://api.telegram.org/bot690429248:AAHbW1LajpqyLmn_m5qCV4UtjpBKzXNhPoc/getupdates");
                    JObject json = JObject.Parse(responseString);

                    for (int i = 0; i < ((JArray)json["result"]).Count; i++)
                        this.messages.Add(JsonConvert.DeserializeObject<Message>((json["result"][i]["message"]).ToString()));

                    for (int i = 0; i < this.messages.Count; i++)
                    {
                        bool reply = false;

                        if (MESSAGE_ID < this.messages[i].message_id && FORWARD_FROM_MESSAGE_ID < this.messages[i].forward_from_message_id)
                        {
                            for (int j = 0; j < this.forward_from_message_id_list.Count; j++)
                            {
                                if (this.messages[i].forward_from_message_id == this.forward_from_message_id_list[j])
                                {
                                    reply = true;
                                    this.replay_id_position.Add(j);
                                }
                            }

                            MESSAGE_ID = this.messages[i].message_id;
                            if (this.messages[i].caption != null || this.messages[i].text != null && reply)
                            {
                                if (!reply)
                                {
                                    this.txt = this.messages[i].caption;
                                    string[] arr = this.txt.Split(separator);

                                    for (int j = 0; j < arr.Length; j++)
                                    {
                                        if (arr[j].ToLower() == "sell" || arr[j].ToLower() == "buy")
                                        {
                                            string file_id = json["result"][i]["message"]["photo"][0]["file_id"].ToString();
                                            responseString = client.DownloadString("https://api.telegram.org/bot690429248:AAHbW1LajpqyLmn_m5qCV4UtjpBKzXNhPoc/getFile?file_id=" + file_id);
                                            var json2 = JObject.Parse(responseString);
                                            string file_path = json2["result"]["file_path"].ToString();
                                            client.DownloadFile("https://api.telegram.org/file/bot690429248:AAHbW1LajpqyLmn_m5qCV4UtjpBKzXNhPoc/" + file_path, @"C:\Users\damia\Desktop\Newfolder\Picture" + this.photo_number.ToString() + ".jpg");
                                            ExecuteTrade(arr, this.messages[i].forward_from_message_id);
                                            this.photo_number++;
                                            break;
                                        }
                                    }
                                }

                                else
                                {
                                    this.txt = this.messages[i].text + " reply";
                                    string[] arr = this.txt.Split(separator);

                                    for (int j = 0; j < arr.Length - 1; j++)
                                    {
                                        if (arr[j].ToLower() == "new" && arr[j + 1].ToLower() == "sl" && arr.Length < 8)
                                        {
                                            ModifyStopLoss();
                                        }

                                        if (arr[j].ToLower() == "pips-" || arr[j].ToLower() == "pip-" || arr[j] == "00" && arr.Length < 8)
                                        {
                                            CloseTrade();
                                        }
                                    }

                                    this.replay_id_position.Clear();
                                }
                            }
                        }
                    }
                }
            }

            catch (Exception e)
            {
                TextForRevision(e.ToString());
            }

            DeletePictures();
            CheckingForCloseOrders();
            Thread.Sleep(30000);
            return 0;
        }

        /// <summary>
        /// Get all the information of the trade and execute it
        /// </summary>
        /// <param name="arr"></param>
        /// <param name="id"></param>
        void ExecuteTrade(string[] arr, int id)
        {
            var Results = OCR.Read(@"C:\Users\damia\Desktop\Newfolder\Picture" + this.photo_number.ToString() + ".jpg");
            this.pair = Results.Text;
            string[] numbers = Regex.Split(this.txt, @"[^0-9\.]+");
            GetPair();
            OrderOperation(arr);
            GetNumbers(numbers);
            int n = 0;

            if (this.order_operation == 0)
            {
                BuyPosition();
                for (int i = 0; i < 3; i++)
                {
                    n = OrderSend(this.pair, OP_BUY, LOTS * lots_percent[i], Ask, 100, this.trade_position[4], this.trade_position[i + 1]);
                    if (n > 0)
                    {
                        this.ticket_list.Add(n);
                        this.forward_from_message_id_list.Add(id);
                    }
                }
            }

            else
            {
                SellPosition();
                for (int i = 0; i < 3; i++)
                { 
                    n = OrderSend(this.pair, OP_SELL, LOTS * lots_percent[i], Ask, 100, this.trade_position[4], this.trade_position[i + 1]);
                    if (n > 0)
                    {
                        this.ticket_list.Add(n);
                        this.forward_from_message_id_list.Add(id);
                    }
                }
            }

            WriteTxt();
            if (n == -1)
                TextForRevision("The Order did not execute for pair " + this.pair);

            this.pair = "";
            this.trade_position.Clear();
            this.order_operation = -1;
        }

        /// <summary>
        /// Gets the pair of a trade
        /// </summary>
        void GetPair()
        {
            int aux = 0;
            int amount = 0;
            int pos = 0;
            for (int i = 0; i < pair_array.Length; i++)
            {
                int puntero = 0;

                for (int j = 0; j < this.pair.Length; j++)
                {

                    if (pair_array[i][puntero] == this.pair.ToUpper()[j])
                    {
                        aux++;
                        puntero++;
                    }

                }

                if (aux > amount)
                {
                    amount = aux;
                    pos = i;

                }

                aux = 0;
            }

            this.pair = pair_array[pos];
        }

        /// <summary>
        /// Gets the order type of a trade <Sell> or <Buy>
        /// </summary>
        /// <param name="arr"></param>
        void OrderOperation(string[]arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].ToLower() == "buy")
                {
                    this.order_operation = 0;
                    break;
                }

                if (arr[i].ToLower() == "sell")
                {
                    this.order_operation = 1;
                    break;
                }
            }
        }

        /// <summary>
        /// Gets the positions of a trade from an array of string and parse to a list of double
        /// </summary>
        /// <param name="numbers"></param>
        void GetNumbers(string[]numbers)
        {
            for (int i = 0; i < numbers.Length; i++)
            {
                try
                {
                    if(numbers[i] != "")
                        this.trade_position.Add(Convert.ToDouble(numbers[i]));
                }

                catch
                {
                    if (this.trade_position.Count == 0)
                    {
                        if (this.order_operation == 0)
                        {
                            double vask = MarketInfo(this.pair, MODE_ASK);
                            this.trade_position.Add(vask);
                        }

                        if (this.order_operation == 1)
                        {
                            double vbid = MarketInfo(this.pair, MODE_BID);
                            this.trade_position.Add(vbid);
                        }
                    }

                    TextForRevision("Exception in <GET TRADE POSITIONS >");
                    continue;
                }
            }
        }

        /// <summary>
        /// Verify if the numbers are correct for a buy trade
        /// </summary>
        void BuyPosition()
        {
            this.trade_position[0] = MarketInfo(this.pair, MODE_ASK); 

            if (this.trade_position[0] > this.trade_position[1])
                this.trade_position[1] = 0;

            if (this.trade_position[0] > this.trade_position[2])
                this.trade_position[2] = 0;

            if (this.trade_position[0] > this.trade_position[3])
                this.trade_position[3] = 0;

            if (this.trade_position[0] < this.trade_position[4])
                this.trade_position[4] = 0;
        }

        /// <summary>
        /// Verify if the numbers are correct for a sell trade
        /// </summary>
        void SellPosition()
        {
            this.trade_position[0] = MarketInfo(this.pair, MODE_BID);

            if (this.trade_position[0] < this.trade_position[1])
                this.trade_position[1] = 0;

            if (this.trade_position[0] < this.trade_position[2])
                this.trade_position[2] = 0;

            if (this.trade_position[0] < this.trade_position[3])
                this.trade_position[3] = 0;

            if (this.trade_position[0] > this.trade_position[4])
                this.trade_position[4] = 0;
        }

        /// <summary>
        /// There are three trades per pair, when the first trade take profit, 
        /// the stop loss of the remainding trades are modify and put at open price
        /// </summary>
        /// <param name="order"></param>
        void StopLossBreakEven(int order)
        {
            int pos = this.ticket_list.IndexOf(order);
            this.ticket_list.Remove(order);
            this.forward_from_message_id_list.RemoveAt(pos);
            WriteTxt();
            OrderSelect(order, SELECT_BY_TICKET);
            this.pair = OrderSymbol();
            this.order_operation = OrderType();

            for (int i = 0; i < this.ticket_list.Count; i++)
            {
                OrderSelect(this.ticket_list[i], SELECT_BY_TICKET);
                if (OrderSymbol() == this.pair && OrderType() == this.order_operation)
                   OrderModify(this.ticket_list[i], OrderOpenPrice(), OrderOpenPrice(), OrderTakeProfit(), expiration);
                  
            }

            this.pair = "";
            this.order_operation = -1;
        }

        /// <summary>
        /// Delete the downloaded photos from their folder
        /// </summary>
        void DeletePictures()
        {
            string[] filePaths = Directory.GetFiles(@"C:\Users\damia\Desktop\Newfolder\");
            foreach (string filePath in filePaths)
            {
                try
                {
                    File.Delete(filePath);
                }

                catch
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// Checks for any new close order
        /// </summary>
        void CheckingForCloseOrders()
        {
            for (int i = 0; i < this.ticket_list.Count; i++)
            {
                OrderSelect(this.ticket_list[i], SELECT_BY_TICKET);

                // If the value that OrderCloseTime() return is different to <<<1/1/0001 12:00:00 AM>>> the trade is closed

                if (OrderCloseTime() != Convert.ToDateTime("1/1/0001 12:00:00 AM"))
                    StopLossBreakEven(this.ticket_list[i]);

            }
        }

        /// <summary>
        /// Write the data from forward_from_message_id to forwardFromMessageId.txt
        /// </summary>
        void WriteTxt()
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < this.forward_from_message_id_list.Count; i++)
                lines.Add(this.forward_from_message_id_list[i].ToString());

            File.WriteAllLines(file_path, lines);
        }

        /// <summary>
        /// Read the data from forwardFromMessageId.txt and add it to forward_from_message_id
        /// </summary>
        void ReadTxt()
        {
            List<string> lines = File.ReadAllLines(file_path).ToList();
            for (int i = 0; i < lines.Count; i++)
                this.forward_from_message_id_list.Add(double.Parse(lines[i]));

        }

        /// <summary>
        /// Send alert text to my Telegram Chanel
        /// </summary>
        /// <param name="text"></param>
        void TextForRevision(string text)
        {
            string apiToken = "";
            string chatId = "";
            string urlString = "https://api.telegram.org/bot{0}/sendMessage?chat_id={1}&text={2}";

            apiToken = "714493268:AAEIGn-fxh8L_yrEtryy0G2yvXaOOPDDTWU";
            chatId = "474653809";

            text += " M15 VIP GROUP";

            urlString = String.Format(urlString, apiToken, chatId, text);

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
        /// Modify the stop loss becuase the trade is going negative
        /// </summary>
        void ModifyStopLoss()
        {
            string[] numbers = Regex.Split(this.txt, @"[^0-9\.]+");
            double new_stop_loss = 0;

            for (int i = 0; i < numbers.Length; i++)
            {
                if (numbers[i] != "")
                {
                    new_stop_loss = double.Parse(numbers[i]);
                    break;
                }
            }

            for (int i = 0; i < this.replay_id_position.Count; i++)
            {
                OrderSelect(this.ticket_list[this.replay_id_position[i]], SELECT_BY_POS);
                bool value = OrderModify(this.ticket_list[this.replay_id_position[i]],OrderOpenPrice(),new_stop_loss,OrderTakeProfit(),expiration);
                if (!value)
                {
                    TextForRevision("the Stop Loss did not modify for " + OrderSymbol().ToString());
                }
            }
        }

        /// <summary>
        /// Close all trades by decision of the trader.
        /// </summary>
        void CloseTrade()
        {
            for (int i = 0; i < this.replay_id_position.Count; i++)
            {
                OrderSelect(this.ticket_list[this.replay_id_position[i]], SELECT_BY_POS);
                bool value = OrderClose(this.ticket_list[this.replay_id_position[i]], OrderLots(), OrderOpenPrice(), 100);
                if (!value)
                {
                    TextForRevision("the Stop Loss did not modify for " + OrderSymbol().ToString());
                }
            }
        }
    }
}
