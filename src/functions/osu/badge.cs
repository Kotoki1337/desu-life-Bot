﻿using KanonBot.Drivers;
using KanonBot.Message;
using KanonBot.API;
using static KanonBot.functions.Accounts;
using Flurl.Util;
using JetBrains.Annotations;
using System.Security.Cryptography;
using K4os.Hash.xxHash;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using Img = SixLabors.ImageSharp.Image;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Formats.Png;
using System.IO;
using Flurl;
using Flurl.Http;

namespace KanonBot.functions.osubot
{
    public class Badge
    {
        public static async Task Execute(Target target, string cmd)
        {
            // 验证账户
            var AccInfo = GetAccInfo(target);
            Database.Model.User? DBUser;
            DBUser = await Accounts.GetAccount(AccInfo.uid, AccInfo.platform);
            if (DBUser == null)
            // { target.reply("您还没有绑定Kanon账户，请使用!reg 您的邮箱来进行绑定或注册。"); return; }    // 这里引导到绑定osu
            { target.reply("您还没有绑定osu账户，请使用!bind osu 您的osu用户名 来绑定您的osu账户。"); return; }
            string rootCmd, childCmd = "";
            try
            {
                rootCmd = cmd[..cmd.IndexOf(" ")].Trim();
                childCmd = cmd[(cmd.IndexOf(" ") + 1)..].Trim();
            }
            catch { rootCmd = cmd; }
            switch (rootCmd.ToLower())
            {
                case "sudo":
                    await SudoExecute(target, childCmd, AccInfo); return;
                case "set":
                    await Set(target, childCmd, AccInfo); return;
                case "info":
                    await Info(target, childCmd, AccInfo); return;
                case "list":
                    await List(target, AccInfo); return;
                default:
                    target.reply("!badge set/info/list");
                    return;
            }
        }
        private static async Task SudoExecute(Target target, string cmd, AccInfo accinfo)
        {
            var userinfo = await Database.Client.GetUsersByUID(accinfo.uid, accinfo.platform);
            List<string> permissions = new();
            if (userinfo!.permissions!.IndexOf(";") < 1) //一般不会出错，默认就是user
            {
                permissions.Add(userinfo.permissions);
            }
            else
            {
                var t1 = userinfo.permissions.Split(";");
                foreach (var x in t1)
                {
                    permissions.Add(x);
                }
            }
            //检查用户权限
            int permissions_flag = -1;
            foreach (var x in permissions)
            {
                switch (x)
                {
                    case "banned":
                        permissions_flag = -1;
                        break;
                    case "user":
                        if (permissions_flag < 1) permissions_flag = 1;
                        break;
                    case "mod":
                        if (permissions_flag < 2) permissions_flag = 2;
                        break;
                    case "admin":
                        if (permissions_flag < 3) permissions_flag = 3;
                        break;
                    case "system":
                        permissions_flag = -2;
                        break;
                    default:
                        permissions_flag = -1;
                        break;
                }

            }

            if (permissions_flag < 2) return; //权限不够不处理


            //execute
            string rootCmd, childCmd = "";
            try
            {
                rootCmd = cmd[..cmd.IndexOf(" ")].Trim();
                childCmd = cmd[(cmd.IndexOf(" ") + 1)..].Trim();
            }
            catch { rootCmd = cmd; }

            switch (rootCmd)
            {
                case "create":
                    await SudoCreate(target, childCmd); return;
                case "delete":
                    SudoDelete(target, childCmd); return;
                case "getuser":
                    SudoGetUser(target, childCmd); return;
                case "list":
                    //await List(target, accinfo); 
                    return;
                case "addbyemail":
                    await SudoAdd(target, childCmd, 0); return;
                case "addbyoid":
                    await SudoAdd(target, childCmd, 1); return;
                default:
                    return;
            }

        }
        //注：没有完全适配多徽章安装，需要等新面板后再做适配
        private static async Task Set(Target target, string cmd, AccInfo accinfo)
        {
            if (int.TryParse(cmd, out int badgeNum))
            {
                if (badgeNum < 1)
                {
                    target.reply("你提供的badge id不正确，请重新检查。");
                }

                var userinfo = await Database.Client.GetUsersByUID(accinfo.uid, accinfo.platform);
                if (userinfo!.owned_badge_ids == null)
                {
                    target.reply("你还没有牌子呢..."); return;
                }

                //获取已拥有的牌子
                List<string> owned_badges = new();
                if (userinfo.owned_badge_ids.IndexOf(",") < 1)
                {
                    owned_badges.Add(userinfo.owned_badge_ids.Trim());
                }
                else
                {
                    var owned_badges_temp1 = userinfo.owned_badge_ids.Split(",");
                    foreach (var x in owned_badges_temp1)
                        owned_badges.Add(x);
                }

                //获取当前已安装的牌子
                List<string> displayed_badges = new();
                if (userinfo.displayed_badge_ids!.IndexOf(",") < 1)
                {
                    if (userinfo.displayed_badge_ids != null)
                        displayed_badges.Add(userinfo.displayed_badge_ids.Trim());
                }
                else
                {
                    var displayed_badges_temp1 = userinfo!.displayed_badge_ids.Split(",");
                    foreach (var x in displayed_badges_temp1)
                        displayed_badges.Add(x);
                }

                //检查当前badge
                foreach (var x in displayed_badges)
                {
                    if (x == badgeNum.ToString())
                    {
                        target.reply($"你现在的主显badge已经是 {x} 了！"); return;
                    }
                }

                //检查用户是否拥有此badge
                if (owned_badges.Count < badgeNum && badgeNum > 0)
                {
                    target.reply($"你好像没有编号为 {badgeNum} 的badge呢..."); return;
                }

                //设置badge
                //没有完全适配多徽章安装，需要等新面板后再取消注释
                //if (displayed_badges.Count == 0)
                //{
                if (badgeNum > 0)
                {
                    if (await Database.Client.SetDisplayedBadge(userinfo.uid.ToString(), owned_badges[badgeNum - 1]))
                        target.reply($"设置成功");
                    else
                        target.reply($"因数据库原因设置失败，请稍后再试。");
                    return;
                }
                else
                {
                    if (await Database.Client.SetDisplayedBadge(userinfo.uid.ToString(), "-1"))
                        target.reply($"设置成功，已关闭badge显示。");
                    else
                        target.reply($"因数据库原因设置失败，请稍后再试。");
                    return;
                }
                //}
                //else
                //{
                //    string settemp1 = "";
                //    foreach (var x in displayed_badges)
                //        settemp1 += x + ",";
                //    settemp1 += owned_badges[badgeNum - 1];
                //    if (await Database.Client.SetDisplayedBadge(userinfo.uid.ToString(), settemp1))
                //        target.reply($"设置成功");
                //    else
                //        target.reply($"因数据库原因设置失败，请稍后再试。");
                //    return;
                //}
            }
            else
            {
                target.reply("你提供的badge id不正确，请重新检查。");
            }
        }
        private static async Task Info(Target target, string cmd, AccInfo accinfo)
        {
            if (int.TryParse(cmd, out int badgeNum))
            {
                if (badgeNum < 1)
                {
                    target.reply("你提供的badge id不正确，请重新检查。");
                }

                var userinfo = await Database.Client.GetUsersByUID(accinfo.uid, accinfo.platform);
                if (userinfo!.owned_badge_ids == null)
                {
                    target.reply("你还没有牌子呢..."); return;
                }

                //获取已拥有的牌子
                List<string> owned_badges = new();
                if (userinfo.owned_badge_ids.IndexOf(",") < 1)
                {
                    owned_badges.Add(userinfo.owned_badge_ids.Trim());
                }
                else
                {
                    var owned_badges_temp1 = userinfo.owned_badge_ids.Split(",");
                    foreach (var x in owned_badges_temp1)
                        owned_badges.Add(x);
                }

                //检查用户是否拥有此badge
                if (owned_badges.Count < badgeNum)
                {
                    target.reply($"你好像没有编号为 {badgeNum} 的badge呢..."); return;
                }


                //获取badge信息
                var badgeinfo = await Database.Client.GetBadgeInfo(owned_badges[badgeNum - 1]);
                target.reply($"badge信息:\n" +
                    $"名称: {badgeinfo!.name}({badgeinfo.id})\n" +
                    $"中文名称: {badgeinfo.name_chinese}\n" +
                    $"描述: {badgeinfo.description}");
            }
            else
            {
                target.reply("你提供的badge id不正确，请重新检查。");
            }
        }
        private static async Task List(Target target, AccInfo accinfo)
        {
            var userinfo = await Database.Client.GetUsersByUID(accinfo.uid, accinfo.platform);
            if (userinfo!.owned_badge_ids == null)
            {
                target.reply("你还没有牌子呢..."); return;
            }

            //获取已拥有的牌子
            List<string> owned_badges = new();
            if (userinfo.owned_badge_ids.IndexOf(",") < 1)
            {
                owned_badges.Add(userinfo.owned_badge_ids.Trim());
            }
            else
            {
                var owned_badges_temp1 = userinfo.owned_badge_ids.Split(",");
                foreach (var x in owned_badges_temp1)
                    owned_badges.Add(x);
            }

            //获取badge信息
            var msg = $"以下是你拥有的badge列表:";
            for (int i = 0; i < owned_badges.Count; i++)
            {
                var badgeinfo = await Database.Client.GetBadgeInfo(owned_badges[i]);
                msg += $"\n{i + 1}:{badgeinfo!.name_chinese} ({badgeinfo.name})";
            }
            target.reply(msg);
        }

        private static async Task SudoCreate(Target target, string cmd)
        {
            //badge sudo create IMG_URL#英文名称#中文名称#详细信息
            //检查参数数量
            var args = cmd.Split("#");
            if (args.Length < 4)
            {
                target.reply("缺少参数。[!badge sudo create IMG_URL#英文名称#中文名称#详细信息]");
                return;
            }
            //检查URL
            var img_url = args[0].Trim();
            if (!Utils.IsUrl(img_url))
            {
                target.reply("提供的IMG_URL不正确。[!badge sudo create IMG_URL#英文名称#中文名称#详细信息]");
                return;
            }
            //检查badge图片是否符合要求规范 https://desu.life/test/test_badge.png
            //下载图片
            var randomstr = Utils.RandomStr(50);
            await img_url.DownloadFileAsync(@$".\work\tmp\", $"{randomstr}.png");
            var filepath = @$".\work\tmp\{randomstr}.png";
            Img source = Img.Load(filepath);
            if (source.Width / 21.5 != source.Height / 10)
            {
                target.reply("badge尺寸不符合要求，应为 21.5 : 10（推荐为688*320），操作取消。");
                return;
            }
            source.Mutate(x => x.Resize(688, 320));

            //保存badge图片&数据库插入新的badge
            var db_badgeid = await Database.Client.InsertBadge(args[1], args[2], args[3]);
            source.Save($"./work/badges/{db_badgeid}.png");
            await source.SaveAsync($"./work/badges/{db_badgeid}.png", new PngEncoder());
            target.reply($"图片成功上传，新的badgeID为{db_badgeid}");
            source.Dispose();
            File.Delete(filepath);
        }
        private static void SudoDelete(Target target, string cmd)
        {
            //不是真正的删除，而是禁用某个badge，使其无法被检索到
            //以后再说 到真正需要此功能的时候再写
        }
        private static void SudoGetUser(Target target, string cmd)
        {

        }
        private static async Task SudoAdd(Target target, string cmd, int addMethod)
        {
            var args = cmd.Split("#"); //args[0]=badge id      args[1]=user(s)
            var badgeid = args[0].Trim();
            string[] users;
            if (args[1].IndexOf("/") != -1)
                users = args[1].Split("/");
            else
                users = new string[] { args[1] };
            string replymsg;
            List<string> failed_msg;
            Database.Model.BadgeList badge;
            bool skip;
            //0=email 1=oid
            switch (addMethod)
            {
                case 0:
                    #region email
                    //检查各个email是否合法
                    replymsg = "";
                    failed_msg = new();
                    foreach (var user in users)
                        if (!Utils.IsMailAddr(user.Trim()))
                            failed_msg.Add($"{user} 为无效的email，请重新检查。");
                    if (failed_msg.Count > 0)
                    {
                        replymsg += $"检查email有效性失败，共有{failed_msg.Count}个email为无效email，详细信息如下：";
                        foreach (var x in failed_msg)
                            replymsg += $"\r\n{x}";
                        target.reply(replymsg);
                        return;
                    }

                    //检查badge是否合法以及是否存在
                    if (!int.TryParse(badgeid, out _)) { target.reply("badgeid不正确，请重新检查。"); return; }
                    badge = await Database.Client.GetBadgeInfo(badgeid);
                    if (badge == null) { target.reply($"似乎没有badgeid为 {badgeid} 的badge呢。"); return; }

                    target.reply($"开始徽章添加任务。");
                    //添加badge
                    failed_msg = new();
                    foreach (var x in users)
                    {
                        skip = false;
                        var userInfo = await Database.Client.GetUsers(x);
                        if (userInfo == null)
                            failed_msg.Add($"desu.life用户 {x} 未注册desu.life账户或email未绑定，请重新确认");
                        else
                        {
                            //获取已拥有的牌子
                            List<string> owned_badges = new();
                            if (userInfo.owned_badge_ids != null || userInfo.owned_badge_ids != "") //用户没有badge的情况下，直接写入
                            {
                                //用户只有一个badge的时候直接追加
                                if (userInfo.owned_badge_ids!.IndexOf(",") == -1)
                                {
                                    if (userInfo.owned_badge_ids != "")
                                        owned_badges.Add(userInfo.owned_badge_ids.Trim());
                                }
                                else
                                {
                                    //用户有2个或以上badge的情况下，先解析再追加新的badge后写入
                                    var owned_badges_temp1 = userInfo.owned_badge_ids.Split(",");
                                    foreach (var xx in owned_badges_temp1)
                                        owned_badges.Add(xx);
                                }
                                //添加之前先查重
                                foreach (var xx in owned_badges)
                                {
                                    if (xx.IndexOf(badgeid) > -1)
                                    {
                                        skip = true;
                                        failed_msg.Add($"desu.life用户 {x} 已拥有此badge，跳过");
                                        break;
                                    }
                                }
                            }
                            //添加
                            if (!skip)
                            {
                                owned_badges.Add(badgeid);
                                string t = "";
                                foreach (var xxx in owned_badges)
                                    t += xxx + ",";
                                if (!await Database.Client.SetOwnedBadge(x, t[..^1]))
                                    failed_msg.Add($"数据库发生了错误，无法为desu.life用户 {x} 添加badge，请稍后重试");
                            }
                        }
                    }
                    replymsg = "完成。";
                    if (failed_msg.Count > 0)
                    {
                        replymsg += $"\r\n共有{failed_msg.Count}名用户添加失败，错误信息如下：";
                        foreach (var x in failed_msg)
                            replymsg += $"\r\n{x}";
                    }
                    target.reply(replymsg);
                    #endregion
                    break;
                case 1:
                    #region oid
                    //检查各个oid是否合法
                    replymsg = "";
                    failed_msg = new();
                    foreach (var user in users)
                        if (!long.TryParse(user.Trim(), out _))
                            failed_msg.Add($"{user} 为无效的uid，请重新检查。");
                    if (failed_msg.Count > 0)
                    {
                        replymsg += $"检查osu!uid有效性失败，共有{failed_msg.Count}个uid为无效uid，详细信息如下：";
                        foreach (var x in failed_msg)
                            replymsg += $"\r\n{x}";
                        target.reply(replymsg);
                        return;
                    }

                    //检查badge是否合法以及是否存在
                    if (!int.TryParse(badgeid, out _)) { target.reply("badgeid不正确，请重新检查。"); return; }
                    badge = await Database.Client.GetBadgeInfo(badgeid);
                    if (badge == null) { target.reply($"似乎没有badgeid为 {badgeid} 的badge呢。"); return; }

                    target.reply($"开始徽章添加任务。");
                    //添加badge
                    failed_msg = new();
                    foreach (var x in users)
                    {
                        skip = false;
                        var userInfo = await Database.Client.GetUserByOsuUID(long.Parse(x));
                        if (userInfo == null)
                            failed_msg.Add($"osu!用户 {x} 未注册desu.life账户或osu!账户不存在，请重新确认");
                        else
                        {
                            //获取已拥有的牌子
                            List<string> owned_badges = new();
                            if (userInfo.owned_badge_ids != null && userInfo.owned_badge_ids != "") //用户没有badge的情况下，直接写入
                            {
                                //用户只有一个badge的时候直接追加
                                if (userInfo.owned_badge_ids!.IndexOf(",") == -1)
                                {
                                    if (userInfo.owned_badge_ids != "")
                                        owned_badges.Add(userInfo.owned_badge_ids.Trim());
                                }
                                else
                                {
                                    //用户有2个或以上badge的情况下，先解析再追加新的badge后写入
                                    var owned_badges_temp1 = userInfo.owned_badge_ids.Split(",");
                                    foreach (var xx in owned_badges_temp1)
                                        owned_badges.Add(xx);
                                }
                                //添加之前先查重
                                foreach (var xx in owned_badges)
                                {
                                    if (xx.IndexOf(badgeid) > -1)
                                    {
                                        skip = true;
                                        failed_msg.Add($"osu!用户 {x} 已拥有此badge，跳过");
                                        break;
                                    }
                                }
                            }
                            //添加
                            if (!skip)
                            {
                                owned_badges.Add(badgeid);
                                string t = "";
                                foreach (var xxx in owned_badges)
                                {
                                    t += xxx + ",";
                                }

                                if (!await Database.Client.SetOwnedBadgeByOsuUid(x, t[..^1]))
                                    failed_msg.Add($"数据库发生了错误，无法为osu!用户 {x} 添加badge，请稍后重试");
                            }
                        }
                    }
                    replymsg = "完成。";
                    if (failed_msg.Count > 0)
                    {
                        replymsg += $"\r\n共有{failed_msg.Count}名用户添加失败，错误信息如下：";
                        foreach (var x in failed_msg)
                            replymsg += $"\r\n{x}";
                    }
                    target.reply(replymsg);
                    break;
                #endregion
                default:
                    return;
            }

        }
        private static void SudoRemove(Target target, string cmd)
        {

        }
        private static void SudoList(Target target, string cmd)
        {

        }
    }
}
