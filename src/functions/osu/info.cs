﻿using KanonBot.Drivers;
using KanonBot.Message;
using KanonBot.API;

namespace KanonBot.functions.osubot
{
    public class Info
    {
        async public static Task Execute(Target target, string cmd)
        {
            #region 验证
            LegacyImage.Draw.UserPanelData data = new();
            int bannerStatus = 0;
            bool is_bounded = false;
            Database.Model.User? DBUser = null;
            Database.Model.UserOSU? DBOsuInfo = null;
            OSU.Models.User? OnlineOsuInfo;

            // 解析指令
            var command = BotCmdHelper.CmdParser(cmd, BotCmdHelper.FuncType.Info);

            // 解析指令
            if (command.self_query)
            {
                // 验证账户
                var AccInfo = Accounts.GetAccInfo(target);
                DBUser = await Accounts.GetAccount(AccInfo.uid, AccInfo.platform);
                if (DBUser == null)
                { target.reply("您还没有绑定Kanon账户，请使用!reg 您的邮箱来进行绑定或注册。"); return; }
                // 验证osu信息
                var _u = await Database.Client.GetUsersByUID(AccInfo.uid, AccInfo.platform);
                DBOsuInfo = (await Accounts.CheckOsuAccount(_u!.uid))!;
                if (DBOsuInfo == null)
                { target.reply("您还没有绑定osu账户，请使用!bind osu 您的osu用户名来绑定您的osu账户。"); return; }

                command.osu_mode ??= OSU.Enums.ParseMode(DBOsuInfo.osu_mode);

                // 验证osu信息
                OnlineOsuInfo = await OSU.GetUser(DBOsuInfo.osu_uid, command.osu_mode!.Value);
                is_bounded = true;
            }
            else
            {
                // 验证osu信息
                OnlineOsuInfo = await OSU.GetUser(command.osu_username, command.osu_mode ?? OSU.Enums.Mode.OSU);
                is_bounded = false;
            }

            // 验证osu信息
            if (OnlineOsuInfo == null)
            {
                if (is_bounded) { target.reply("被办了。"); return; }
                target.reply("猫猫没有找到此用户。"); return;
            }

            if (!is_bounded) // 未绑定用户回数据库查询找模式
            {
                var temp_uid = await Database.Client.GetOsuUser(OnlineOsuInfo.Id);
                DBOsuInfo = await Accounts.CheckOsuAccount(temp_uid == null ? -1 : temp_uid.uid)!;
                if (DBOsuInfo != null)
                {
                    is_bounded = true;
                    DBUser = await Accounts.GetAccount(OnlineOsuInfo.Id);
                    command.osu_mode ??= OSU.Enums.ParseMode(DBOsuInfo.osu_mode);
                    OnlineOsuInfo = await OSU.GetUser(command.osu_username, command.osu_mode ?? OSU.Enums.Mode.OSU)!;   // 这里正常是能查询到的，所以用非空处理(!)
                }
            }
            #endregion

            #region 获取信息
            data.userInfo = OnlineOsuInfo!;
            data.userInfo.PlayMode = command.osu_mode!.Value;
            // 查询

            if (is_bounded)
            {
                if (command.order_number > 0)
                {
                    // 从数据库取指定天数前的记录
                    (data.daysBefore, data.prevUserInfo) = await Database.Client.GetOsuUserData(DBOsuInfo!.osu_uid, data.userInfo.PlayMode, command.order_number);
                    if (data.daysBefore > 0) ++data.daysBefore;
                }
                else
                {
                    // 从数据库取最近的一次记录
                    try { (data.daysBefore, data.prevUserInfo) = await Database.Client.GetOsuUserData(DBOsuInfo!.osu_uid, data.userInfo.PlayMode, 0);
                    if (data.daysBefore > 0) ++data.daysBefore;
                    }
                    catch { data.daysBefore = 0; }
                }

                switch (DBOsuInfo!.customInfoEngineVer)
                {
                    case 1:
                        //new
                        break;
                    default:
                        //legacy

                        // 取PP+信息
                        data.pplusInfo = new();
                        var d = await Database.Client.GetOsuPPlusData(DBOsuInfo!.osu_uid);
                        if (d != null)
                        {
                            data.pplusInfo = d;
                        }
                        else
                        {
                            // 异步获取osupp数据，下次请求的时候就有了
                            await Task.Run(async () =>
                            {
                                try { await Database.Client.UpdateOsuPPlusData((await API.OSU.TryGetUserPlusData(OnlineOsuInfo!))!.User, OnlineOsuInfo!.Id); }
                                catch { }//更新pp+失败，不返回信息
                            }).ConfigureAwait(false);
                        }

                        var badgeID = DBUser!.displayed_badge_ids;
                        // legacy只取第一个badge
                        if (badgeID != null)
                            try { if (badgeID.IndexOf(",") != -1) badgeID = badgeID[..badgeID.IndexOf(",")]; }
                            catch { badgeID = "-1"; }
                        try { data.badgeId = int.Parse(badgeID!); }
                        catch { data.badgeId = -1; }
                        bannerStatus = DBOsuInfo.customBannerStatus;// 取bannerStatus

                        break;
                }
            }
            else
            {
                // 未绑定用户默认用新面板

            }
            #endregion

            var isDataOfDayAvaiavle = false;
            if (data.daysBefore > 0) isDataOfDayAvaiavle = true;
            MemoryStream img = LegacyImage.Draw.DrawInfo(data, bannerStatus, is_bounded, isDataOfDayAvaiavle);
            img.TryGetBuffer(out ArraySegment<byte> buffer);
            //target.reply(new Chain().msg("test").image(Convert.ToBase64String(buffer.Array!, 0, (int)img.Length), ImageSegment.Type.Base64));
            target.reply(new Chain().image(Convert.ToBase64String(buffer.Array!, 0, (int)img.Length), ImageSegment.Type.Base64));
            //AnnualPass(data.userInfo.userId, data.userInfo.mode, data.userInfo.totalHits); //季票内容
        }
    }
}
