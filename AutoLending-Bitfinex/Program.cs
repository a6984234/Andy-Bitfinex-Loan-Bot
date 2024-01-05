using Bitfinex.Net.Clients;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AutoLending_Bitfinex {
    class Program {
        private static BitfinexRestClient bitfinexRestClient = null;
        /// <summary>
        /// 最低放貸利率
        /// </summary>
        public static  decimal LowestPrice = 0.00025m;
        /// <summary>
        /// 預留資金
        /// </summary>
        public static int SetAsideFunds = 0;
        /// <summary>
        /// 每單最低金額
        /// </summary>
        public static int UnitAmount = 150;
        public static bool RunnerPass;
        static void Main(string[] args) {
            Console.WriteLine("安迪的綠葉放貸機器人 ver 1.04");
            Console.Write("請輸入API 金鑰 : ");
            var key = Console.ReadLine();
            Console.Write("請輸入API 密鑰 : ");
            var scretKey = Console.ReadLine();
            bitfinexRestClient = new BitfinexRestClient(opeiont => {
                opeiont.ApiCredentials = new CryptoExchange.Net.Authentication.ApiCredentials(key, scretKey);
                opeiont.RequestTimeout = TimeSpan.FromSeconds(60);
            });
            bool init = false;
            Task task = null;
            CommonList commenList = new CommonList();
            while (true) {
                if (!init) {
                    task = Task.Run(MainRunner);
                    init = true;
                }
                var enterCommon = Console.ReadLine();
                commenList.ReadCommon(enterCommon);
            }
            async Task MainRunner() {
                while(RunnerPass)
                    await Task.Delay(1000);
                Console.WriteLine("運轉中...");
                //獲取資產餘額
                var data = await bitfinexRestClient.SpotApi.Account.GetBalancesAsync();
                if (data.GetResultOrError(out var data1, out var error)) {
                    var Usd_Remaining = data1.Where(x => x.Type == Bitfinex.Net.Enums.WalletType.Funding && x.Asset == "USD").First()?.Available ?? 0;
                    Usd_Remaining -= SetAsideFunds;//扣掉使用者想要預留的資金
                    //如果USD剩餘有超過150
                    if (Usd_Remaining >= UnitAmount) {
                        if (await GetActiveFundingOffersCount() == 0) {
                            //借出金額
                            var quantity = Usd_Remaining - UnitAmount >= UnitAmount ?
                                    UnitAmount
                                    : Math.Round(Usd_Remaining, 3) > Usd_Remaining ?
                                        Math.Round(Usd_Remaining, 3) - 0.001m :
                                        Math.Round(Usd_Remaining, 3);
                            //限定利率
                            var rate = await GetAvg();
                            //設定日期
                            var period = SetPeriod(rate);
                            if (rate != 0) {
                                var SubmitFundingOffer = await bitfinexRestClient.GeneralApi.Funding.SubmitFundingOfferAsync(Bitfinex.Net.Enums.FundingOrderType.Limit, "fUSD", quantity, rate, period);
                                if (SubmitFundingOffer.GetResultOrError(out var fundingOffer, out var fundingOfferError)) {
                                    Console.WriteLine("已送出融資訂單 , Rate" + rate + " day" + period);
                                } else {
                                    Console.WriteLine("送出融資訂單錯誤!");
                                    Console.WriteLine("SubmitFundingOffer Error" + fundingOfferError.Message);
                                }
                            }
                        }
                    } else {
                        await GetActiveFundingOffersCount();
                    }
                } else {
                    Console.WriteLine("獲取帳戶資訊錯誤!");
                    Console.WriteLine("Get Account Error " + error.Message);
                }
                await DelayMainRunner();

                async Task DelayMainRunner() {
                    await Task.Delay(10000);
                    await MainRunner();
                }
            }
            //檢查是否仍有訂單 無訂單回傳 
            async Task<int> GetActiveFundingOffersCount() {
                var getActiveFundingOffers = await bitfinexRestClient.GeneralApi.Funding.GetActiveFundingOffersAsync("fUSD");
                if (getActiveFundingOffers.GetResultOrError(out var result, out var offersError)) {
                    if (result.Any() && Math.Round(await GetAvg(), 6) != Math.Round(result.First().Rate, 6)) {
                        Console.WriteLine("已有訂單 利率:" + result.First().Rate + "天數:" + result.First().Period);
                        await bitfinexRestClient.GeneralApi.Funding.CancelAllFundingOffersAsync();
                        Console.WriteLine("更新訂單");
                    }
                    return result.Count();
                } else {
                    Console.WriteLine("獲取訂單錯誤 " + offersError.Message);
                    return -1;
                }
            }
            //獲取價格平均值
            async Task<decimal> GetAvg() {
                var _klineData = await bitfinexRestClient.SpotApi.ExchangeData.GetKlinesAsync(
                            "fUSD",
                            Bitfinex.Net.Enums.KlineInterval.ThirtyMinutes,
                            fundingPeriod: "p2",
                            startTime: DateTime.Now.ToUniversalTime().AddHours(-12));
                if (_klineData.GetResultOrError(out var data, out var error)) {
                    var highprice = data.OrderByDescending(x => x.HighPrice).Select(x => x.HighPrice).ToList().GetRange(0, 11);
                    var avg = highprice.Average();
                    Console.WriteLine(avg);
                    //檢查最新價格是不是比avg還高
                    var tradeHistory = await bitfinexRestClient.SpotApi.ExchangeData.GetTradeHistoryAsync("fUSD", startTime: DateTime.Now.ToUniversalTime().AddMinutes(-30));
                    if (tradeHistory.GetResultOrError(out var tradeData, out var tradeError)) {
                        if (tradeData.First().Price > avg) {
                            avg = tradeData.First().Price;
                            Console.WriteLine("Avg被市場價格刷新 " + avg);
                        }
                    } else {
                        Console.WriteLine("Trade history error " + tradeError.Message);
                        return 0;
                    }
                    Console.WriteLine("AVG " + avg);
                    return avg > LowestPrice ? avg : LowestPrice;
                } else
                    return 0;
            }
            int SetPeriod(decimal rate) {
                if (rate < 0.0003m)
                    return 2;
                if (rate < 0.0004m)
                    return 7;
                return 30;
            }
        }
    }
}
