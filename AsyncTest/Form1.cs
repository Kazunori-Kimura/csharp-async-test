using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AsyncTest
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// append log delegate
        /// </summary>
        /// <param name="text"></param>
        delegate void AppendLogDelegate(string text);

        /// <summary>
        /// TaskのCancelTokenSource
        /// </summary>
        private CancellationTokenSource tokenSource = null;

        /// <summary>
        /// Task list
        /// </summary>
        private List<Task> taskList = new List<Task>();



        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // タイマー停止状態とする
            this.timer1.Enabled = false;
            SetControlsStatus(false);
        }

        /// <summary>
        /// 開始
        /// </summary>
        private void StartTimer()
        {
            try
            {
                AppendLog("StartTimer.");
                // フォームの無効化
                SetControlsStatus(true);
                // 時間を取得
                int interval = int.Parse(this.txtInterval.Text);
                timer1.Interval = interval;
                // タイマー開始
                timer1.Enabled = true;

                // cancel token source
                tokenSource = new CancellationTokenSource();
            }
            catch (Exception e)
            {
                this.txtLog.AppendText(e.StackTrace);
            }
        }

        /// <summary>
        /// 中断
        /// </summary>
        private void StopTimer()
        {
            try
            {
                AppendLog("StopTimer.");
                // タスクのキャンセル
                tokenSource.Cancel();
                // timerを停止
                timer1.Enabled = false;
                // フォームの有効化
                SetControlsStatus(false);

                // すべてのタスクがキャンセルされたことを確認
                while(true)
                {
                    Thread.Sleep(300); // 300ms待機

                    foreach (var task in taskList)
                    {
                        if (!task.IsCanceled && !task.IsCompleted)
                        {
                            continue;
                        }
                    }
                    break;
                }

                // タスクリストをクリア
                taskList.Clear();
                // tokenResourceを削除
                tokenSource.Dispose();
            }
            catch (Exception e)
            {
                this.txtLog.AppendText(e.StackTrace);
            }
        }

        /// <summary>
        /// 処理開始・中断時にコンポーネントの有効・無効をセット
        /// </summary>
        /// <param name="isStarted"></param>
        private void SetControlsStatus(bool isStarted)
        {
            this.txtTaskNum.Enabled = !isStarted;
            this.txtInterval.Enabled = !isStarted;
            this.btnStart.Enabled = !isStarted;
            this.btnCancel.Enabled = isStarted;
        }

        /// <summary>
        /// 開始
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStart_Click(object sender, EventArgs e)
        {
            StartTimer();
        }

        /// <summary>
        /// 中断
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            StopTimer();
        }

        /// <summary>
        /// clear
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnClear_Click(object sender, EventArgs e)
        {
            txtLog.Text = string.Empty;
        }

        /// <summary>
        /// テキスト追加
        /// </summary>
        /// <param name="text"></param>
        private void AppendLog(string text)
        {
            txtLog.AppendText(string.Format("[{0}] {1}\n",
                DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"),
                text));
        }


        /// <summary>
        /// ランダム時間停止するだけのタスク
        /// </summary>
        /// <param name="taskId"></param>
        /// <returns></returns>
        private Task RandomSleepTask(int taskId)
        {
            var token = tokenSource.Token;
            
            return Task.Run(async () =>
            {
                // Sleep時間を取得 (3,000 ~ 15,000)
                var rand = new Random(taskId);
                int waitTime = rand.Next(3000, 15000);
                // start message
                string msg = string.Format(@"task{0} started: sleepTime={1}", taskId, waitTime);
                AppendLogDelegate appendLog = new AppendLogDelegate(AppendLog);
                this.Invoke(appendLog, new Object[] { msg });

                // sleep
                await Task.Delay(waitTime, token);

                // cancelされた？
                token.ThrowIfCancellationRequested();

                msg = string.Format(@"task{0} finished.", taskId);
                this.Invoke(appendLog, new Object[] { msg });
            }, token).ContinueWith((t) =>
            {
                if (t.IsCanceled)
                {
                    // キャンセルされたときの処理
                    AppendLogDelegate appendLog = new AppendLogDelegate(AppendLog);
                    this.Invoke(appendLog, new Object[] { string.Format(@"task{0} canceled.", taskId) });
                }
            });
        }

        /// <summary>
        /// タイマー処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                AppendLog("start timer1_Tick.");

                int taskNum = int.Parse(this.txtTaskNum.Text);
                for (int i = 0; i < taskNum; i++)
                {
                    if (taskList.Count <= i)
                    {
                        // タスクを実行
                        var t = RandomSleepTask(i);
                        // リストに追加
                        taskList.Add(t);
                    }
                    else
                    {
                        var t = taskList[i];
                        if (!t.Status.Equals(TaskStatus.RanToCompletion))
                        {
                            // 前回のタスクが実行中ならスキップする
                            AppendLog(string.Format("task{0} is running.", i));
                            continue;
                        }

                        // タスクを実行
                        t = RandomSleepTask(i);
                        // リストに保持
                        taskList[i] = t;
                    }
                }
            }
            catch (Exception exp)
            {
                AppendLog(exp.StackTrace);
            }
        }
    }
}
