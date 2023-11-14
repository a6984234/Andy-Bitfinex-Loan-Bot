using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoLending_Bitfinex {
    internal class CommonList {
        public List<Common> commons=new List<Common>();
        /// <summary>
        /// 指令列表
        /// </summary>
        public CommonList() {
            commons.Add(new Common(
                "Clear", 0,
                () => {
                    Console.Clear();
                }));
            commons.Add(new Common(
                "SetLowestPrice", 1,
                () => {
                    Program.RunnerPass = true;
                    Console.WriteLine("請輸入要設定的最低利率\n" +
                        "利率換算方式 0.00025 = 0.025% (請以 *0.00025* 這個格式來設定)\n" +
                        "請注意不要超過7% Bitfinex不允許 ");
                    var setLowestRate = Console.ReadLine();
                    if (Decimal.TryParse(setLowestRate, out var resule)) {
                        if (resule > 0.07m || resule <= 0) {
                            Console.WriteLine("更新失敗\n" +
                                "請注意不要超過7%的限制!");
                        } else {
                            Program.LowestPrice = resule;
                            Console.WriteLine("已更新最低利率");
                        }
                    } else {
                        Console.WriteLine("設定失敗");
                    }
                    Program.RunnerPass= false;
                }));
        }
        /// <summary>
        /// 讀取指令並執行
        /// </summary>
        /// <param name="common"></param>
        public void ReadCommon(string common) {
            var getCommons = commons.Where(x => {
                return x.CommonName.ToLower() == common.ToLower()
                    || x.ShortcutKeys?.ToString() == common.ToLower();
            });
            if (getCommons.Count() <= 0)
                Console.WriteLine("指令錯誤!");
            else
                getCommons.First().RunCommon();
        }
    }
    public class Common {
        private string common;
        private int? shortcutKeys;

        public Common(string _common, int? _shortcutKeys, Action runAction) {
            common = _common;
            shortcutKeys = _shortcutKeys;
            RunAction = runAction;
        }

        public string CommonName => common;
        public int? ShortcutKeys => shortcutKeys;
        private Action RunAction { get; set; }
        public Action RunCommon => RunAction;
    }
}
