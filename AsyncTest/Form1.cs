using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AsyncTest
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// 別スレッドからフォームを操作するための Delegate
        /// </summary>
        /// <param name="text"></param>
        delegate void AppendLogDelegate(string text);

        /// <summary>
        /// TaskのCancelTokenSource
        /// </summary>
        private CancellationTokenSource tokenSource = null;

        /// <summary>
        /// タスクの実行状態を確認するため、実行されたタスクをリストに格納
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
        /// タイマー開始
        /// </summary>
        private void StartTimer()
        {
            try
            {
                AppendLog("タイマー開始");
                
                // 時間を取得
                int interval = int.Parse(this.txtInterval.Text);
                timer1.Interval = interval;

                // タイマー開始
                timer1.Enabled = true;

                // cancel token source を生成
                tokenSource = new CancellationTokenSource();
            }
            catch (Exception e)
            {
                this.txtLog.AppendText(e.StackTrace);
            }
        }

        /// <summary>
        /// タイマー中断
        /// </summary>
        private async void StopTimer()
        {
            try
            {
                AppendLog("タイマーストップ処理 開始");
                
                // timerを停止
                timer1.Enabled = false;

                // タスクのキャンセル
                tokenSource.Cancel();

                // すべてのタスクがキャンセルされたことを確認する
                await Task.Run(() =>
                {
                    bool existsRunningTask = true;
                    while (existsRunningTask)
                    {
                        existsRunningTask = false;

                        // 各タスクの実行状態をチェック
                        foreach (var task in taskList)
                        {
                            if (!task.IsCanceled && !task.IsCompleted)
                            {
                                // まだタスクが完了していなければチェックを続行
                                existsRunningTask = true;
                                break;
                            }
                        }

                        if (existsRunningTask)
                        {
                            // 300ms待機 (300msに根拠はない)
                            Thread.Sleep(300);
                        }
                    }

                    // 全タスク終了
                    AppendLogDelegate appendLog = new AppendLogDelegate(AppendLog);
                    this.Invoke(appendLog, new Object[] { "全タスク終了" });
                });
                
                // タスクリストをクリア
                taskList.Clear();

                // tokenSourceを削除
                tokenSource.Dispose();

                AppendLog("タイマーストップ処理 終了");
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
        /// 開始ボタンのクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStart_Click(object sender, EventArgs e)
        {
            // フォームの無効化
            SetControlsStatus(true);

            StartTimer();
        }

        /// <summary>
        /// 中断ボタンのクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            StopTimer();

            // フォームの有効化
            SetControlsStatus(false);
        }

        /// <summary>
        /// クリアボタンのクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnClear_Click(object sender, EventArgs e)
        {
            txtLog.Text = string.Empty;
        }

        /// <summary>
        /// 日時を付与してログ領域にテキストを追加する
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
            // CancellationTokenを取得
            var token = tokenSource.Token;
            
            return Task.Run(async () =>
            {
                // Sleep時間を取得 (3,000 ~ 15,000)
                var rand = new Random(DateTime.Now.Millisecond + taskId);
                int waitTime = rand.Next(3000, 15000);

                // 開始ログの表示
                string msg = string.Format(@"task{0} 開始: sleepTime={1}", taskId, waitTime);
                AppendLogDelegate appendLog = new AppendLogDelegate(AppendLog);
                this.Invoke(appendLog, new Object[] { msg });

                // 指定時間 Sleep する
                await Task.Delay(waitTime, token);

                // cancelされた？
                token.ThrowIfCancellationRequested();

                // 終了ログの表示
                msg = string.Format(@"task{0} 終了", taskId);
                this.Invoke(appendLog, new Object[] { msg });

            }, token).ContinueWith((t) =>
            {
                if (t.IsCanceled)
                {
                    // キャンセルされたときの処理
                    // -> キャンセルログの表示
                    AppendLogDelegate appendLog = new AppendLogDelegate(AppendLog);
                    this.Invoke(appendLog, new Object[] { string.Format(@"task{0} キャンセル", taskId) });
                }
            });
        }

        /// <summary>
        /// タイマーから定期的に呼ばれる処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                AppendLog("タイマー定期実行開始");

                // 実行するタスク数
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
                            AppendLog(string.Format("task{0} は実行中のため、スキップ", i));
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
