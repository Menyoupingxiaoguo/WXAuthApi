using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WXAuthApi.common;

namespace WXAuthApi.Controllers
{
    /// <summary>
    /// 微信公众号用户信息获取
    /// </summary>
    public class AuthController : Controller
    {
        private static string appId = ConfigurationManager.AppSettings["appid"];
        private static string secret = ConfigurationManager.AppSettings["appsecret"];
        /// <summary>
        /// 微信公众号引导页 使用微信AccessToken获取微信用户信息，但不包括用户UnionID信息
        /// </summary>
        /// <returns>成功时，返回带有用户信息的URL并重定向</returns>
        public ActionResult GetUserInfo1()
        {
            string code = Request.QueryString["code"];
            string state = Request.QueryString["state"];
            try
            {
                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(state))
                {
                    OAuthToken oauthToken = JsonConvert.DeserializeObject<OAuthToken>(new WXHelper().Request(string.Format("https://api.weixin.qq.com/sns/oauth2/access_token?appid={0}&secret={1}&code={2}&grant_type=authorization_code", appId, secret, code), "", "GET"));

                    string accesstoken = string.Empty;
                    AccessToken token = JsonConvert.DeserializeObject<AccessToken>(new WXHelper().Request(string.Format("https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={0}&secret={1}", appId, secret), "", "GET"));

                    if (token != null && !string.IsNullOrEmpty(token.access_token))
                    {
                        accesstoken = token.access_token;
                    }

                    if (oauthToken != null && !string.IsNullOrEmpty(oauthToken.openid))
                    {
                        OAuthUserInfo userInfo = JsonConvert.DeserializeObject<OAuthUserInfo>(new WXHelper().Request(string.Format("https://api.weixin.qq.com/cgi-bin/user/info?access_token={0}&openid={1}&lang=zh_CN", accesstoken, oauthToken.openid), "", "GET"));
                        if (userInfo != null)
                        {
                            ViewData["headImage"] = userInfo.headimgurl;
                            ViewData["openid"] = userInfo.openid;
                            ViewData["nickName"] = userInfo.nickname;
                            if (userInfo.sex == 0)
                            {
                                ViewData["sex"] = "未知";
                            }
                            else if (userInfo.sex == 1)
                            {
                                ViewData["sex"] = "男";
                            }
                            else
                            {
                                ViewData["sex"] = "女";
                            }
                            ViewData["province"] = userInfo.province;
                            ViewData["city"] = userInfo.city;

                            object objUrl = CacheHelper.GetCache(state);
                            if (objUrl != null)
                            {
                                UriBuilder URL = new UriBuilder(objUrl.ToString());
                                if (!string.IsNullOrEmpty(URL.Query))
                                    URL.Query += string.Format("&unionid={0}&openid={1}&appid={2}",
                                    userInfo.unionid, userInfo.openid, appId);
                                else
                                    URL.Query += string.Format("unionid={0}&openid={1}&appid={2}",
                                    userInfo.unionid, userInfo.openid, appId);

                                return Redirect(URL.ToString());
                            }
                            else
                                ViewData["errmsg"] = "重定向URL获取失败！";
                        }
                        else
                        {
                            ViewData["errmsg"] = "用户信息获取失败！";
                        }
                    }
                    else
                    {
                        ViewData["errmsg"] = "Token获取失败！";
                    }
                }
                else
                {
                    ViewData["errmsg"] = "用户code获取失败！";
                }
            }
            catch (Exception ex)
            {
                ViewData["errmsg"] = ex.Message;
            }

            return View();
        }
        /// <summary>
        /// 微信公众号引导页 使用微信SNSToken获取微信用户信息，包括用户UnionID信息
        /// </summary>
        /// <returns>成功时，返回带有用户信息的URL并重定向</returns>
        public ActionResult GetUserInfo2()
        {
            string code = Request.QueryString["code"];
            string state = Request.QueryString["state"];
            try
            {
                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(state))
                {
                    WXHelper WXHelper = new WXHelper();
                    string strGetSnsToken = WXHelper.Request(string.Format("https://api.weixin.qq.com/sns/oauth2/access_token?appid={0}&secret={1}&code={2}&grant_type=authorization_code", appId, secret, code), "", "GET");
                    JObject jo1 = JsonConvert.DeserializeObject<JObject>(strGetSnsToken);

                    string strResult = WXHelper.Request(string.Format("https://api.weixin.qq.com/sns/userinfo?access_token={0}&openid={1}&lang=zh_CN ", jo1["access_token"].ToString(), jo1["openid"].ToString()), "", "GET");
                    OAuthUserInfo userInfo = JsonConvert.DeserializeObject<OAuthUserInfo>(strResult);
                    if (userInfo != null)
                    {
                        object objUrl = CacheHelper.GetCache(state);
                        if (objUrl != null)
                        {
                            UriBuilder URL = new UriBuilder(objUrl.ToString());
                            string directUrl = URL.ToString();
                            directUrl += string.Format("?openid={0}&nickname={1}&sex={2}&province={3}&city={4}&country={5}&headimgurl={6}&unionid={7}",
                            userInfo.openid, userInfo.nickname, userInfo.sex, userInfo.province, userInfo.city, userInfo.country, userInfo.headimgurl, userInfo.unionid);

                            Response.Redirect(directUrl);
                        }
                    }
                }
                else
                {
                    ViewData["errmsg"] = "用户code获取失败！";
                }
            }
            catch (Exception ex)
            {
                ViewData["errmsg"] = ex.Message;
            }

            return View();
        }
        /// <summary>
        /// 微信公众号引导页
        /// </summary>
        /// <param name="url">微信前端传递的跳转url</param>
        /// <returns>成功时，重定向至获取用户信息</returns>
        public ActionResult Index(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                url = WXHelper.DecodeBase64(url);
                string state = EncryptHelper.MD5Encrypt(url);
                //判断url根据MD5生成的密文在缓存中是否存在
                object objUrl = CacheHelper.GetCache(state);
                if (objUrl == null)
                    CacheHelper.AddCache(state, url, 5);//不存在则将url和对应的密文存储在缓存中，存储时长为5分钟
                else
                    CacheHelper.SetCache(state, url, 5);//存在则将url和对应的密文在缓存中更新，更新存储时长为5分钟

                return Redirect(string.Format("https://open.weixin.qq.com/connect/oauth2/authorize?appid={0}&redirect_uri={1}&response_type=code&scope=snsapi_base&state={2}#wechat_redirect", appId, ConfigurationManager.AppSettings["zp_apppath"] + "/Recruitment/Auth/GetUserInfo", state));
            }
            else
                ViewData["errmsg"] = "重定向url不能为空！";

            return View();
        }
    }
}