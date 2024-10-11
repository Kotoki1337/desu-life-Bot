using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KanonBot.API;

namespace KanonBot
{
public static class BotCmdHelper
{
    public struct BotParameter
    {
        public OSU.Enums.Mode? osu_mode;
        public string osu_username, osu_mods, match_name;           //用于获取具体要查询的模式，未提供返回osu
        public long osu_user_id, bid;
        public int order_number;//用于info的查询n天之前、pr，bp的序号，score的bid，如未提供则返回0
        public bool lazer; //是否获取lazer数据
        public bool self_query;
        public Option<int> StartAt;
        public Option<int> EndAt;
        public string extra_args;
    }

    public enum FuncType
    {
        Info,
        BestPerformance,
        Recent,
        PassRecent,
        Score,
        Leeway,
        RoleCost,
        BPList,
    }

    public static BotParameter CmdParser(string? cmd, FuncType type)
    {
        cmd = cmd?.Trim().ToLower();
        BotParameter param = new() { lazer = false, self_query = false };
        if (!String.IsNullOrEmpty(cmd)) {
            string arg1 = "", arg2 = "", arg3 = "", arg4 = "";
            int section = 0;
            // 解析所有可能的参数
            for (var i = 0; i < cmd.Length; i++) {
                if (cmd[i] == ':') {
                    section = 1;
                } else if (cmd[i] == '#') {
                    section = 2;
                } else if (cmd[i] == '+') {
                    section = 3;
                } else if (cmd[i] == '&') {
                    section = 4;
                }

                switch (section)
                {
                    case 1:
                        arg2 += cmd[i]; // :
                        break;
                    case 2:
                        arg3 += cmd[i]; // #
                        break;
                    case 3:
                        arg4 += cmd[i]; // +
                        break;
                    case 4:
                        param.lazer = true;
                        break;
                    default:
                        arg1 += cmd[i];
                        break;
                }
            }
            arg1 = arg1.Trim();
            arg2 = arg2.Trim();
            arg3 = arg3.Trim();
            arg4 = arg4.Trim();
            // 处理info解析
            if (type == FuncType.BPList) {
                // arg1 = username
                // arg2 = osu_mode
                // arg3 = osu_days_before_to_query
                // #1-100
                if (arg2 != "") {
                    try {
                        param.osu_mode = OSU.Enums.Int2Mode(int.Parse(arg2[1..]));
                    } catch {
                        param.osu_mode = null;
                    }
                }
                if (arg3 != "") {
                    param.osu_username = arg1;
                    var tmp = arg3[1..];
                    if (tmp.Contains('-')) {
                        var t = tmp.Split('-');
                        param.StartAt = parseInt(t[0].Trim());  // StartAt
                        param.EndAt = parseInt(t[1].Trim());  // EndAt
                    } else { //只指定了最大值
                        param.StartAt = Some(1);  // StartAt
                        param.EndAt = parseInt(tmp.Trim());  // EndAt
                    }
                } else {
                    param.self_query = true;
                    if (arg1.Contains('-')) {
                        var t = arg1.Split('-');
                        param.StartAt = parseInt(t[0].Trim());  // StartAt
                        param.EndAt = parseInt(t[1].Trim());  // EndAt
                    } else { //只指定了最大值
                        param.StartAt = Some(1);  // StartAt
                        param.EndAt = parseInt(arg1.Trim());  // EndAt
                    }
                }
            } else if (type == FuncType.Info) {
                // arg1 = username
                // arg2 = osu_mode
                // arg3 = osu_days_before_to_query
                param.osu_username = arg1;
                if (arg2 != "") {
                    try {
                        param.osu_mode = OSU.Enums.Int2Mode(int.Parse(arg2[1..]));
                    } catch {
                        param.osu_mode = null;
                    }
                }

                if (arg3 == "" || param.osu_username == null) {
                    param.order_number = 0;
                } else {
                    try {
                        param.order_number = int.Parse(arg3[1..]);
                    } catch {
                        param.order_number = 0;
                    }
                }

                if (String.IsNullOrEmpty(param.osu_username)) {
                    param.self_query = true;
                }
            } else if (type == FuncType.BestPerformance) {
                // bp
                // arg1 = username / order_number
                // arg2 = osu_mode
                // arg3 = order_number (序号)
                if (!int.TryParse(arg1, out param.order_number)) {
                    param.osu_username = arg1;

                    //成绩必须为1
                    if (arg3 == "" || param.osu_username == null) {
                        param.order_number = 1;
                    } else {
                        try {
                            var t = param.order_number = int.Parse(arg3[1..]);
                            if (t > 100 || t < 1) param.order_number = 1;
                        } catch {
                            param.order_number = 0;
                        }
                    }

                    if (String.IsNullOrEmpty(param.osu_username)) {
                        param.self_query = true;
                    }
                } else {
                    param.self_query = true;
                }

                if (arg2 != "") {
                    try {
                        param.osu_mode = OSU.Enums.Int2Mode(int.Parse(arg2[1..]));
                    } catch {
                        param.osu_mode = null;
                    }
                }
            } else if (type == FuncType.Recent || type == FuncType.PassRecent) {
                // 处理pr/re解析
                // arg1 = username
                // arg2 = osu_mode
                // arg3 = order_number (序号)
                param.osu_username = arg1;
                if (arg2 != "") {
                    try {
                        param.osu_mode = OSU.Enums.Int2Mode(int.Parse(arg2[1..]));
                    } catch {
                        param.osu_mode = null;
                    }
                }

                // 成绩必须为1
                if (arg3 == "" || param.osu_username == null) {
                    param.order_number = 1;
                } else {
                    try {
                        var t = param.order_number = int.Parse(arg3[1..]);
                        if (t > 100 || t < 1) {
                            param.order_number = 1;
                        }
                    } catch {
                        param.order_number = 0;
                    }
                }

                if (String.IsNullOrEmpty(param.osu_username)) {
                    param.self_query = true;
                }
            } else if (type == FuncType.Score) {
                // 处理score解析
                // arg1 = username
                // arg2 = osu_mode :
                // arg3 = bid #
                // arg4 = mods +

                //没提供用户名
                if (arg3 == "" || param.osu_username == null) {
                    param.osu_username = "";

                     //bid必须有效，否则返回 -1
                    if (arg1 == "") {
                        param.order_number = -1;
                    } else {
                        try {
                            var index = int.Parse(arg1);
                            param.order_number = index < 1 ? -1 : index;
                            param.extra_args = arg1;
                        } catch {
                            param.order_number = -1;
                        }
                    }
                } else {
                    //提供了用户名
                    param.osu_username = arg1;

                     //bid必须有效，否则返回 -1
                    if (arg3 == "" || param.osu_username == null) {
                        param.order_number = -1;
                    } else {
                        try {
                            var index = int.Parse(arg3[1..]);
                            param.order_number = index < 1 ? -1 : index;
                            param.extra_args = arg3[1..];
                        } catch {
                            param.order_number = -1;
                        }
                    }
                }

                if (arg2 != "") {
                    try {
                        param.osu_mode = OSU.Enums.Int2Mode(int.Parse(arg2[1..]));
                    } catch {
                        param.osu_mode = null;
                    }
                }

                param.osu_mods = arg4 != "" ? arg4[1..] : "";
                if (String.IsNullOrEmpty(param.osu_username)) {
                    param.self_query = true;
                }
            } else if (type == FuncType.Leeway) {
                // arg1 = bid
                // arg2 = osu_mode
                // arg3 =
                // arg4 = mods

                // 若bid为空，返回0
                if (arg1 == "" || param.osu_username == null) {
                    param.order_number = 0;
                } else {
                    try {
                        var index = int.Parse(arg1);
                        param.order_number = index < 1 ? -1 : index;
                    } catch {
                        param.order_number = 0;
                    }
                }

                if (arg2 != "") {
                    try {
                        param.osu_mode = OSU.Enums.Int2Mode(int.Parse(arg2[1..]));
                    } catch {
                        param.osu_mode = null;
                    }
                }

                param.osu_mods = arg4 != "" ? arg4[1..] : "";
                param.self_query = true; // 只查自己
            } else if (type == FuncType.RoleCost) {
                // arg1 = match name
                // arg3 = username #
                if (arg3 != "") {
                    param.osu_username = arg3[1..];
                }

                param.match_name = arg1;
                
                if (String.IsNullOrEmpty(param.osu_username)) {
                    param.self_query = true;
                }
            }
        } else {
            param.self_query = true;
            if (type == FuncType.Info) {
                param.order_number = 0; //由于cmd为空，所以默认查询当日信息
            } else if (type == FuncType.Score) {
                param.order_number = -1; //由于cmd为空，所以没有bid等关键信息，返回错误
            } else if (type == FuncType.Recent || type == FuncType.PassRecent || type == FuncType.BestPerformance) {
                param.order_number = 1; //由于cmd为空，所以默认返回第一张谱面成绩
            }
        }
        return param;
    }
}
}
