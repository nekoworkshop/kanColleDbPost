﻿using Fiddler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace KanColleDbPost
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
			this.ShowInTaskbar = false;
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			FiddlerApplication.AfterSessionComplete += FiddlerApplication_AfterSessionComplete;
			Application.ApplicationExit += Application_ApplicationExit;
		}

		void Application_ApplicationExit(object sender, EventArgs e)
		{
			if (isCapture)
			{
				// Fiddlerのシャットダウン
				FiddlerApplication.Shutdown();
			}
			global::KanColleDbPost.Properties.Settings.Default.Save();
		}

		public enum UrlType
		{
			PORT,
			SHIP2,
			SHIP3,
			SLOT_ITEM,
			KDOCK,
			MAPINFO,
			CHANGE,
			CREATESHIP,
			GETSHIP,
			CREATEITEM,
			START,
			NEXT,
			SELECT_EVENTMAP_RANK,
			BATTLE,
			BATTLE_MIDNIGHT,
			BATTLE_SP_MIDNIGHT,
			BATTLE_NIGHT_TO_DAY,
			BATTLERESULT,
			COMBINED_BATTLE,
			COMBINED_BATTLE_AIR,
			COMBINED_BATTLE_MIDNIGHT,
			COMBINED_BATTLE_RESULT,
			AIRBATTLE,
			COMBINED_BATTLE_WATER,
			COMBINED_BATTLE_SP_MIDNIGHT,
			//MASTER,
		};

		public Dictionary<UrlType, string> urls = new Dictionary<UrlType, string>()
        {
            { UrlType.PORT,                     "api_port/port"                       },
            { UrlType.SHIP2,                    "api_get_member/ship2"                },
            { UrlType.SHIP3,                    "api_get_member/ship3"                },
            { UrlType.SLOT_ITEM,                "api_get_member/slot_item"            },
            { UrlType.KDOCK,                    "api_get_member/kdock"                },
            { UrlType.MAPINFO,                  "api_get_member/mapinfo"              },
            { UrlType.CHANGE,                   "api_req_hensei/change"               },
            { UrlType.CREATESHIP,               "api_req_kousyou/createship"          },
            { UrlType.GETSHIP,                  "api_req_kousyou/getship"             },
            { UrlType.CREATEITEM,               "api_req_kousyou/createitem"          },
            { UrlType.START,                    "api_req_map/start"                   },
            { UrlType.NEXT,                     "api_req_map/next"                    },
			{ UrlType.SELECT_EVENTMAP_RANK,     "api_req_map/select_eventmap_rank"    }, 
            { UrlType.BATTLE,                   "api_req_sortie/battle"               },
            { UrlType.BATTLE_MIDNIGHT,          "api_req_battle_midnight/battle"      },
            { UrlType.BATTLE_SP_MIDNIGHT,       "api_req_battle_midnight/sp_midnight" },
            { UrlType.BATTLE_NIGHT_TO_DAY,      "api_req_sortie/night_to_day"         },
            { UrlType.BATTLERESULT,             "api_req_sortie/battleresult"         },
            { UrlType.COMBINED_BATTLE,          "api_req_combined_battle/battle"      },
            { UrlType.COMBINED_BATTLE_AIR,      "api_req_combined_battle/airbattle"   },
            { UrlType.COMBINED_BATTLE_MIDNIGHT, "api_req_combined_battle/midnight_battle"},
            { UrlType.COMBINED_BATTLE_RESULT,   "api_req_combined_battle/battleresult"},
            { UrlType.AIRBATTLE,                "api_req_sortie/airbattle"            },
            { UrlType.COMBINED_BATTLE_WATER,    "api_req_combined_battle/battle_water"},
            { UrlType.COMBINED_BATTLE_SP_MIDNIGHT,"api_req_combined_battle/sp_midnight"},
			//{ UrlType.MASTER,                   "api_start2"                          },
        };

		private bool isCapture = false;

		void FiddlerApplication_AfterSessionComplete(Session oSession)
		{
			if (oSession.PathAndQuery.StartsWith("/kcsapi") &&
				oSession.oResponse.MIMEType.Equals("text/plain"))
			{
				Task.Factory.StartNew(() =>
				{
					string url = oSession.fullUrl;
					foreach (KeyValuePair<UrlType, string> kvp in urls)
					{
						if (url.IndexOf(kvp.Value) > 0)
						{
							string responseBody = oSession.GetResponseBodyAsString();
							responseBody.Replace("svdata=", "");

							string str = "Post server from " + url + "\n";
							AppendText(str);

							string res = PostServer(oSession);
							str = "Post response : " + res + "\n";
							AppendText(str);
							return;
						}
					}
					if (checkBox1.Checked)
					{
						AppendText(url + "\n");
					}
				});
			}
		}

		private string PostServer(Session oSession)
		{
			string token = textBox2.Text;                   // TODO: ユーザー毎のトークンを設定
			string agent = "";          // TODO: アプリ毎のトークンを設定
			string url = oSession.fullUrl;
			string requestBody = HttpUtility.HtmlDecode(oSession.GetRequestBodyAsString());
			requestBody = Regex.Replace(requestBody, @"&api(_|%5F)token=[0-9a-f]+|api(_|%5F)token=[0-9a-f]+&?", "");	// api_tokenを送信しないように削除
			string responseBody = oSession.GetResponseBodyAsString();
			responseBody.Replace("svdata=", "");

			try
			{
				WebRequest req = WebRequest.Create("http://api.kancolle-db.net/2/");
				req.Method = "POST";
				req.ContentType = "application/x-www-form-urlencoded";

				System.Text.Encoding enc = System.Text.Encoding.GetEncoding("utf-8");
				string postdata =
					  "token=" + HttpUtility.UrlEncode(token) + "&"
					+ "agent=" + HttpUtility.UrlEncode(agent) + "&"
					+ "url=" + HttpUtility.UrlEncode(url) + "&"
					+ "requestbody=" + HttpUtility.UrlEncode(requestBody) + "&"
					+ "responsebody=" + HttpUtility.UrlEncode(responseBody);
				byte[] postDataBytes = System.Text.Encoding.ASCII.GetBytes(postdata);
				req.ContentLength = postDataBytes.Length;

				Stream reqStream = req.GetRequestStream();
				reqStream.Write(postDataBytes, 0, postDataBytes.Length);
				reqStream.Close();

				WebResponse res = req.GetResponse();
				HttpWebResponse httpRes = (HttpWebResponse)res;
				Stream resStream = res.GetResponseStream();
				StreamReader sr = new StreamReader(resStream, enc);
				string response = sr.ReadToEnd();
				sr.Close();
				return oSession.responseCode + ": " + response;
			}
			catch (WebException ex)
			{
				if (ex.Status == WebExceptionStatus.ProtocolError)
				{
					HttpWebResponse error = (HttpWebResponse)ex.Response;
					return error.ResponseUri + " " + oSession.responseCode + ": " + error.StatusDescription;
				}
				return ex.Message;
			}
		}

		/// <summary>
		/// キャプチャ開始
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button1_Click(object sender, EventArgs e)
		{
			if (!isCapture)
			{
				FiddlerApplication.Startup(8877, FiddlerCoreStartupFlags.ChainToUpstreamGateway | FiddlerCoreStartupFlags.RegisterAsSystemProxy | FiddlerCoreStartupFlags.OptimizeThreadPool | FiddlerCoreStartupFlags.MonitorAllConnections);
				isCapture = true;
				AppendText("----- Capture start\n");
				button1.Text = "停止";
			}
			else
			{
				AppendText("----- Capture stop\n");
				FiddlerApplication.Shutdown();
				isCapture = false;
				button1.Text = "開始";
			}
		}


		// Windowsフォームコントロールに対して非同期な呼び出しを行うためのデリゲート
		delegate void SetTextCallback(string text);

		private void AppendText(string text)
		{
			// 呼び出し元のコントロールのスレッドが異なるか確認をする
			if (this.textBox1.InvokeRequired)
			{
				// 同一メソッドへのコールバックを作成する
				SetTextCallback delegateMethod = new SetTextCallback(AppendText);

				// コントロールの親のInvoke()メソッドを呼び出すことで、呼び出し元の
				// コントロールのスレッドでこのメソッドを実行する
				this.Invoke(delegateMethod, new object[] { text });
			}
			else
			{
				// コントロールを直接呼び出す
				this.textBox1.AppendText(text);
				this.textBox1.SelectionStart = textBox1.Text.Length;
				this.textBox1.ScrollToCaret();
			}
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			// トレイリストのアイコンを非表示にする  
			notifyIcon1.Visible = false;
		}

		private void Form1_ClientSizeChanged(object sender, EventArgs e)
		{
			if (this.WindowState == System.Windows.Forms.FormWindowState.Minimized)
			{
				// フォームが最小化の状態であればフォームを非表示にする  
				this.Hide();
				// トレイリストのアイコンを表示する  
				notifyIcon1.Visible = true;
			}
		}

		private void notifyIcon1_DoubleClick(object sender, EventArgs e)
		{
			// フォームを表示する  
			this.Visible = true;
			// 現在の状態が最小化の状態であれば通常の状態に戻す  
			if (this.WindowState == FormWindowState.Minimized)
			{
				this.WindowState = FormWindowState.Normal;
			}
			// フォームをアクティブにする  
			this.Activate();
		}

		private void toolStripMenuItem1_Click(object sender, EventArgs e)
		{
			notifyIcon1_DoubleClick(sender, e);
		}

		private void toolStripMenuItem2_Click(object sender, EventArgs e)
		{
			this.Close();
		}


	}
}
