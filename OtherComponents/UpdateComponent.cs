using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Deployment.Application;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows;
using System.IO;
using System.Windows.Threading;
using System.Threading;

namespace SuperScrub837.OtherComponents
{
    /// <summary>
    /// This is the component of the software that will allow rolling revisions as will inevitably be needed.
    /// </summary>
    public static class UpdateComponent
    {
        public static string updateOrigin;
        private enum UpdateStatuses
        {
            NoUpdateAvailable,
            UpdateAvailable,
            UpdateRequired,
            NotDeployedViaClickOnce,
            DeploymentDownloadException,
            InvalidDeploymentException,
            InvalidOperationException
        }

        public static MainWindow main;

        public static BackgroundWorker bgUpdate;

        /// <summary>
        /// Will be executed when update work needs to be done
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static void checkUpdates(object sender, DoWorkEventArgs e)
        {
            UpdateCheckInfo info = null;

            // Check if the application was deployed via ClickOnce.
            if (!ApplicationDeployment.IsNetworkDeployed)
            {
                e.Result = UpdateStatuses.NotDeployedViaClickOnce;
                return;
            }

            ApplicationDeployment updateCheck = ApplicationDeployment.CurrentDeployment;

            try
            {
                info = updateCheck.CheckForDetailedUpdate();
            }
            catch (DeploymentDownloadException dde)
            {
                e.Result = UpdateStatuses.DeploymentDownloadException;
                return;
            }
            catch (InvalidDeploymentException ide)
            {
                e.Result = UpdateStatuses.InvalidDeploymentException;
                return;
            }
            catch (InvalidOperationException ioe)
            {
                e.Result = UpdateStatuses.InvalidOperationException;
                return;
            }

            if (info.UpdateAvailable)
                if (info.IsUpdateRequired)
                    e.Result = UpdateStatuses.UpdateRequired;
                else
                    e.Result = UpdateStatuses.UpdateAvailable;
            else
                e.Result = UpdateStatuses.NoUpdateAvailable;
        }

        /// <summary>
        /// Will be executed once it's complete...
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static void noteUpdates(object sender, RunWorkerCompletedEventArgs e)
        {
            switch ((UpdateStatuses)e.Result)
            {
                case UpdateStatuses.NoUpdateAvailable:
                    // No update available, do nothing - notify IF the origin is the update button only
                    if(updateOrigin == "UpdateButton")
                        System.Windows.MessageBox.Show("No updates right now!");
                    break;
                case UpdateStatuses.UpdateAvailable:
                    DialogResult dialogResult = System.Windows.Forms.MessageBox.Show("An update is available. Would you like to update the application now?", "Update available", MessageBoxButtons.OKCancel);
                    if (dialogResult.ToString() == "OK")
                    {
                        //BackgroundWorker bgUpdate = new BackgroundWorker();
                        UpdateProgress updateNotify = new UpdateProgress();
                        //bgUpdate.WorkerReportsProgress = true;
                        //bgUpdate.DoWork += (uptSender, uptE) => { UpdateApplication(); };
                        //bgUpdate.WorkerReportsProgress = true;

                        //bgUpdate.ProgressChanged += (progSender, progE) => { updateNotify.updateProgress.Value = progE.ProgressPercentage; };
                        //bgUpdate.RunWorkerCompleted += (comSender, comE) =>
                        //{
                        //    updateNotify.Close();
                        //    applicationUpdated();
                        //};
                        updateNotify.Show();
                        //bgUpdate.RunWorkerAsync();
                        var progress = new Progress<double>(s => { updateNotify.updateProgress.Value = s; });
                        Action<Object> updateResult = o => applicationUpdated();
                        Task.Run(() => UpdateApplication(progress));
                        //UpdateApplication();
                    }
                    break;
                case UpdateStatuses.UpdateRequired:
                    System.Windows.Forms.MessageBox.Show("A required update is available, which will be installed now", "Update available", MessageBoxButtons.OK);
                    //BackgroundWorker bgUpdate = new BackgroundWorker();
                    UpdateProgress updateNotifyR = new UpdateProgress();
                    //bgUpdate.WorkerReportsProgress = true;
                    //bgUpdate.DoWork += (uptSender, uptE) => { UpdateApplication(); };
                    //bgUpdate.WorkerReportsProgress = true;

                    //bgUpdate.ProgressChanged += (progSender, progE) => { updateNotify.updateProgress.Value = progE.ProgressPercentage; };
                    //bgUpdate.RunWorkerCompleted += (comSender, comE) =>
                    //{
                    //    updateNotify.Close();
                    //    applicationUpdated();
                    //};
                    updateNotifyR.Show();
                    //bgUpdate.RunWorkerAsync();
                    var progressR = new Progress<double>(s => { updateNotifyR.updateProgress.Value = s; });
                    Action<Object> updateResultR = o => applicationUpdated();
                    Task.Run(() => UpdateApplication(progressR));
                    //UpdateApplication();
                    break;
                case UpdateStatuses.NotDeployedViaClickOnce:
                    if (updateOrigin == "UpdateButton")
                        System.Windows.MessageBox.Show("Is this deployed via ClickOnce?");
                    break;
                case UpdateStatuses.DeploymentDownloadException:
                    System.Windows.MessageBox.Show("Whoops, couldn't retrieve info on this app...");
                    break;
                case UpdateStatuses.InvalidDeploymentException:
                    System.Windows.MessageBox.Show("Cannot check for a new version. ClickOnce deployment is corrupt!");
                    break;
                case UpdateStatuses.InvalidOperationException:
                    System.Windows.MessageBox.Show("This application cannot be updated. It is likely not a ClickOnce application. Message" + e.Result.ToString());
                    File.AppendAllText("failuredump.txt", "\r\n--------------\r\n" + e.Result.ToString());
                    break;
                default:
                    //this default case should NEVER happen.
                    System.Windows.MessageBox.Show("Huh?");
                    break;
            }
        }

        private static Task UpdateApplication(IProgress<double> progress = null)
        {
            try
            {
                ApplicationDeployment updateCheck = ApplicationDeployment.CurrentDeployment;
                //BackgroundWorker bgWorker = new BackgroundWorker();
                //UpdateProgress updateNotify = new UpdateProgress();
                //updateNotify.Show();
                //bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(UpdateComponent.noteUpdates);
                //bgWorker.RunWorkerAsync();


                //updateNotify.Dispatcher.InvokeAsync(() =>
                // {
                //     updateCheck.UpdateProgressChanged += (s, e) =>
                //     {
                //         updateNotify.updateProgress.Value = e.ProgressPercentage;

                //     };
                //     updateCheck.UpdateCompleted += (s, e) =>
                //     {
                //         updateNotify.Close();
                //         applicationUpdated();

                //     };

                //});
                
            updateCheck.UpdateProgressChanged += (s, e) =>
                 {
                     progress?.Report(e.ProgressPercentage);

                 };
                updateCheck.UpdateCompleted += (s, e) =>
                {
                    //updateNotify.Close();
                    applicationUpdated();

                };
                //updateNotify.Show();
                updateCheck.UpdateAsync();
                //progress?.Report(updateCheck.);
                return null;
            }
            catch (DeploymentDownloadException dde)
            {
                System.Windows.MessageBox.Show("Cannot install the latest version of the application. Please check your network connection, or try again later. Error: " + dde);
                return null;
            }
        }

        private static void applicationUpdateProgress(object sender, DeploymentProgressChangedEventArgs evt)
        {
            bgUpdate.ReportProgress(evt.ProgressPercentage);
        }

        private static void applicationUpdated()
        {
            
            System.Windows.MessageBox.Show("The application has been upgraded, and will now restart. Check the 'About' section to see what's new!");
            //System.Windows.Application.Current.MainWindow.Close();
            System.Windows.Forms.Application.Restart();
            System.Windows.Application.Current.Shutdown();
            //return null;
        }
    }
}
