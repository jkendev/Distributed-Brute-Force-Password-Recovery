using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.Threading;
using System.ComponentModel;
using RecoveryManagerLibrary;   //DLL reference.

namespace PasswordCrackerGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml of the password cracker. 
    /// Manager communicates to GUI via callback interface: IRecoveryManagerGUICallback
    /// </summary>
    public partial class MainWindow : Window, IRecoveryManagerGUICallback
    {
        char[] charSet = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };   //now I know my abc
        
        private NetTcpBinding tcpBinding;
        private String recovManagerURL = "net.tcp://localhost:5002/RecovManager";   //URL of manager.
        private ChannelFactory<IRecoveryManagerGUI> rmFactory;
        private IRecoveryManagerGUI manager;                    //Manager remote server object

        //Boolean controls:
        private Boolean windowLoaded = false;                   //Whether window loaded or not.
        private Boolean connected = false;                      //whether connected to Manager or not.
        private Boolean passwordChosen = false;                 //Whether an encrypted pw has been chosen.
        private Boolean isCustom = false;                       //Whether user specified or not.
        private Boolean pwFound = false;                        //Whether password found or not.
        private Boolean exit = false;                           //Upon exit, this is true.
        private Boolean isRecovering = false;                   //Whether a job is in progress or not.
        
        
        private String customPassword;                          //A custom plaintext password, chosen by user. 

        private long totalPossible;                             //Total possible permutations of the password.

        //These variables left in to showcase extendability. Eg, in future versions, give user chance to select which chars to use. (Not implemented yet).
        private Boolean digitsSelected = true;                                      //digits 0-9 checkbox selected.
        private Boolean capitolsSelected = true;                                    //letters A-Z checkbox selected.
        private Boolean lowerCaseSelected = true;                                   //letters a-z checkbox selected.

        /// <summary>
        ///             Constructor of main window.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            windowLoaded = true;
        }

        /// <summary>
        ///             Function that is called after window constructed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Need to disable the checkboxes because they are not changeable in v1.0
            checkBoxDigits.IsEnabled = false;
            digitsSelected = true;

            checkBoxCapitols.IsEnabled = false;
            capitolsSelected = true;

            checkBoxLowerCase.IsEnabled = false;
            lowerCaseSelected = true;

            checkBoxSpecialChars.IsEnabled = false;

            //Disable some controls.
            textBoxLength.IsReadOnly = true;
            btnEncrypt.IsEnabled = false;
            btnGenerateRandom.IsEnabled = false;
            btnEncrypt.IsEnabled = false;
            textBoxCustomPassword.IsEnabled = false;
            txtEncryptedPW.IsEnabled = false;
            btnBeginRecov.IsEnabled = false;
            

            setTotalPossiblePermutation(Int32.Parse(textBoxLength.Text));
        }

        /********************************************************************************************************************************************************************/
        /************************************************ IRecoveryManagerGUICallback Interface Methods *********************************************************************/
        /********************************************************************************************************************************************************************/

        /// <summary>
        ///             Used to tell the GUI how many services connected to the Manager.
        /// </summary>
        /// <param name="numConnected">     Number of connected services. </param>
        public void setNumConnected(int numConnected)
        {
            outNumConnected.Content = "" + numConnected;
        }

        /// <summary>
        ///             Used to alert GUI that password was found.
        /// </summary>
        /// <param name="password">     The plain text password.</param>
        public void passwordFound(String password)
        {
            MessageBox.Show("Password Found! <" + password + ">", "Finished!");
            outPassword.Content = "Password Found: " + password;
            isRecovering = false;
            btnBeginRecov.Content = "Begin Recovery";
            btnBeginRecov.IsEnabled = false;
            progressBar.Value = 0;
            if (isCustom)
            {
                btnEncrypt.IsEnabled = true;
                btnGenerateRandom.IsEnabled = false;
                textBoxCustomPassword.IsEnabled = true;
                btnLengthDown.IsEnabled = false;
                btnLengthUp.IsEnabled = false;
            }
            else
            {
                btnEncrypt.IsEnabled = false;
                btnGenerateRandom.IsEnabled = true;
                textBoxCustomPassword.IsEnabled = false;
                btnLengthDown.IsEnabled = true;
                btnLengthUp.IsEnabled = true;
            }
        }

        /// <summary>
        ///             Callback function to update GUI about stats of the job. Occurs once every second in v1.0
        /// </summary>
        /// <param name="numConnected">     Number of connected services </param>
        /// <param name="numTested">        Number of tested passwords </param>
        /// <param name="rate">             APPROX Rate in passwords/second that has been tested.</param>
        /// <param name="elapsedSeconds">   Time since job started. </param>
        public void updateJobStats(int numConnected, long numTested, long rate, long elapsedSeconds)
        {

            Dispatcher.Invoke((Action)delegate()
            {
                outNumConnected.Content = numConnected;
                lblTotalTested.Content = "" + numTested + "/" + totalPossible;
                outTestedPerSec.Content = "" + rate + " passwords per second";

                if (rate != 0)
                {
                    long secToComplete = ((totalPossible - numTested) / rate);
                    TimeSpan timeLeftSpan = TimeSpan.FromSeconds(secToComplete);
                    //Took this line of code from: http://stackoverflow.com/questions/9993883/convert-milliseconds-to-human-readable-time-lapse
                    String timeLeft = string.Format("{0:D2}h:{1:D2}m:{2:D2}s", timeLeftSpan.Hours, timeLeftSpan.Minutes, timeLeftSpan.Seconds);
                    outTimeRemaining.Content = timeLeft;
                }

                progressBar.Value = numTested;
                double percent = (((double)numTested / (double)totalPossible) * (double)100);
                outPercentDone.Content = "" + string.Format("{0:0.00}", percent) + "%";

                TimeSpan timeTakenSpan = TimeSpan.FromMilliseconds(elapsedSeconds * 1000);
                string timeTaken = string.Format("{0:D2}h:{1:D2}m:{2:D2}s",
                                        timeTakenSpan.Hours,
                                        timeTakenSpan.Minutes,
                                        timeTakenSpan.Seconds);

                outLapsedTime.Content = timeTaken;
            });
        }


        /********************************************************************************************************************************************************************/
        /******************************************************************** Other Functions *******************************************************************************/
        /********************************************************************************************************************************************************************/

        /// <summary>
        ///             Function called when window closed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            exit = true;    //Breaks out of diagnostics loop
            if(manager != null)
            {
                try
                {
                    manager.GUIDisconnect();
                }
                catch(Exception ex)
                {
                    //No alert, user has closed!
                }
            }
        }

        /// <summary>
        ///             Function to update GUI as user enters custom password.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void customTextBoxChanged(object sender, RoutedEventArgs e)
        {
            if (windowLoaded)
            {
                int length = textBoxCustomPassword.Text.Length;
                if (length > 0 && length <= 10)
                {
                    setTotalPossiblePermutation(textBoxCustomPassword.Text.Length);
                    btnEncrypt.IsEnabled = true;
                }
                else if( length > 10)
                {
                    
                    MessageBox.Show("Error: custom password must be smaller than 10");
                    textBoxCustomPassword.Text = textBoxCustomPassword.Text.Substring(0, 10);
                }
                else if(length == 0)
                {
                    btnEncrypt.IsEnabled = false;

                }
                
            }
        }

        /// <summary>
        ///             Client side validation of custom password to reduce network traffic!
        ///             Server side validation is also implemented.
        /// </summary>
        /// <param name="customPW">     A custom plain text password to test the recovery process. </param>
        /// <returns>   True when the password contained valid characters, false otherwise. </returns>
        private Boolean validateCustomPW(String customPW)
        {
            
            Boolean outcome = false;
            String regex = "^[a-zA-Z0-9_]*$";   
            if(digitsSelected && capitolsSelected && lowerCaseSelected)         //Made true in constructor. This code is really just proof of concept that the program could be extended to include other chars.
            {
                regex = "^[a-zA-Z0-9_]*$";
            }
            
            if (System.Text.RegularExpressions.Regex.IsMatch(customPW, regex))
            {
                outcome = true;
            }

            return outcome;
        }



        /// <summary>
        ///             Ping functioin.
        /// </summary>
        /// <returns></returns>
        public String ping()
        {
            return "pong from GUI";
        }

        

        

        /// <summary>
        ///             Function associated with btnConnect button.
        ///             Connects GUI to manager.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if(!connected)
            {
                tcpBinding = new NetTcpBinding();

                IRecoveryManagerGUICallback callBackObject = this;

                //Now create the Data server object via a factory:
                try
                {
                    rmFactory = new DuplexChannelFactory<IRecoveryManagerGUI>(callBackObject, tcpBinding, recovManagerURL);  //Sets up the channel factory with above created tcp binding and url. 
                    manager = rmFactory.CreateChannel();                                                                     //Creates the channel for client-server communication.   
                    Boolean successfulConnection = manager.addGUIClient();
                    if (successfulConnection)
                    {
                        outServerStatus.Content = "Connected";
                        btnConnect.Content = "Disconnect";
                        connected = true;
                        
                        if((Boolean)radioGenerateRandom.IsChecked)
                        {
                            btnGenerateRandom.IsEnabled = true;
                        }
                        
                        

                        if(passwordChosen)
                        {
                            btnBeginRecov.IsEnabled = true;
                        }

                        if(manager.jobInProgress())
                        {
                            MessageBox.Show("Connected: Job already in progress");
                            
                        }

                    }
                    else
                    {
                        MessageBox.Show("Error: a gui client is already connected. Can only be one GUI at a time");
                        rmFactory.Close();  //TODO: exceptions
                    }
                    
                }

                catch (CommunicationException ce)
                {
                    //Console.WriteLine("***ERROR***: Could not ping the data server. Data server appears to be down! Message:" + ce.Message);
                    //throw new FaultException("Data Server appears to be down or unreachable. Please try again at a later date.");

                }
                catch (TimeoutException te)
                {
                    Console.WriteLine("***WARNING***: Server timed out - could not initialise connection to DATA server");
                    //throw new FaultException("Data Server appears to be down or unreachable. Please try again at a later date.");
                    //Not sure if this would happen.
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Important Message");
                }
            }
            else //Then its a disconnect button (button text will say so)
            {
                //TODO: exception handling.
                try
                {
                    manager.GUIDisconnect();
                    rmFactory.Close();
                    btnConnect.Content = "Connect";
                    outServerStatus.Content = "Disconnected";
                    btnBeginRecov.IsEnabled = false;
                    connected = false;
                }
                catch(TimeoutException ex)
                {
                    MessageBox.Show("Could not disconnect from server due to timeout");
                }
                catch(CommunicationException ex)
                {
                    MessageBox.Show("Could not disconnect from server due to communication error:\n" + ex.Message);
                }
                catch(Exception ex)
                {
                    MessageBox.Show("Could not dicsonnect from server due to unknow nerror:\n" + ex.Message);
                }
            }
            
        }

        /// <summary>
        ///             Function associated with radio button radioCustomPassword.
        ///             Modifies GUI to allow user to enter a custom password.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioCustomPassword_Checked(object sender, RoutedEventArgs e)
        {
            if(windowLoaded && !isRecovering)
            {
                btnEncrypt.IsEnabled = true;
                btnGenerateRandom.IsEnabled = false;
                textBoxCustomPassword.IsEnabled = true;
                btnLengthDown.IsEnabled = false;
                btnLengthUp.IsEnabled = false;


                int length = textBoxCustomPassword.Text.Length;
                if(length > 0)
                {
                    setTotalPossiblePermutation(length);
                }
            }
            
        }

        /// <summary>
        ///             Function associated with radio button radioGenerateRandom
        ///             Modifies GUI to allow user to specify random password of selected length
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioGenerateRandom_Checked(object sender, RoutedEventArgs e)
        {
            if(windowLoaded && !isRecovering)
            {
                btnEncrypt.IsEnabled = false;
                btnGenerateRandom.IsEnabled = true;
                textBoxCustomPassword.IsEnabled = false;
                btnLengthDown.IsEnabled = true;
                btnLengthUp.IsEnabled = true;

                int length = Int32.Parse(textBoxLength.Text);
                setTotalPossiblePermutation(length);

            }
            
        }


        /// <summary>
        ///             Function associated with btnBegin button.
        ///             Kickstarts the recovery process by calling manager.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBeginRecov_Click(object sender, RoutedEventArgs e)
        {
            if(!isRecovering)
            {
                if (passwordChosen && isCustom)
                {

                    try
                    {
                        manager.begin();
                        isRecovering = true;

                        btnLengthDown.IsEnabled = false;
                        btnLengthUp.IsEnabled = false;
                        btnBeginRecov.Content = "Cancel Job";
                    }
                    catch (TimeoutException ex)
                    {
                        MessageBox.Show("Error: could not begin recovery process: request timed out.\n" + ex.Message);
                    }
                    catch (CommunicationException ex)
                    {
                        MessageBox.Show("Error: could not begin recovery process: request caused an error:\n" + ex.Message);
                    }
                    
                    
                }
                else if (passwordChosen && !isCustom)
                {
                    try
                    {
                        String encrypted = "";
                        
                        manager.begin();
                        isRecovering = true;
                    
                        btnGenerateRandom.IsEnabled = false;
                        btnLengthDown.IsEnabled = false;
                        btnLengthUp.IsEnabled = false;
                        btnBeginRecov.Content = "Cancel Job";
                        
                    }
                    catch(TimeoutException ex)
                    {
                        MessageBox.Show("Error: could not begin recovery process: request timed out.\n" + ex.Message);
                    }
                    catch(CommunicationException ex)
                    {
                        MessageBox.Show("Error: could not begin recovery process: request caused an error:\n" + ex.Message);
                    }
                    
                }
            }
            else
            {
                try
                {
                    manager.cancelJob();
                    btnBeginRecov.Content = "Begin Recovery";
                    isRecovering = false;
                }
                catch(TimeoutException ex)
                {
                    MessageBox.Show("Error: could not cancel recovery process: request timed out.\n" + ex.Message);
                }
                catch(CommunicationException ex)
                {
                    MessageBox.Show("Error: could not cancel recovery process: request caused error:\n" + ex.Message);
                }
            }
              
        }

        
       

        /// <summary>
        ///             Encrypts a plaintext custom password. Bonus functionality.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnEncrypt_Click(object sender, RoutedEventArgs e)
        {
            String custom = textBoxCustomPassword.Text;
            String encryptedPW = "";
            if(validateCustomPW(custom))
            {
                try
                {
                    Boolean outcome = manager.setCustomPassword(custom, ref encryptedPW);
                    if(outcome)
                    {
                        txtEncryptedPW.Text = encryptedPW;
                        btnBeginRecov.IsEnabled = true;
                        passwordChosen = true;
                        isCustom = true;
                        customPassword = custom;
                        outPlainTextLength.Content = "" + custom.Length;
                    }
                    else
                    {
                        MessageBox.Show("Error: custom password did not contain only valid characters");
                    }
                   
                }
                catch(TimeoutException ex)
                {
                    MessageBox.Show("Error: could not set custom password:\n" + ex.Message);
                }
                catch(CommunicationException ex)
                {
                    MessageBox.Show("Error: could not set custom password:\n" + ex.Message);
                }
                

            }
            else
            {
                MessageBox.Show("The entered password contains an invalid character. Must be composed from selected characters");
            }
        }

        /// <summary>
        ///             Generates a new random password in default charset.
        ///             Generation done remotely!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnGenerateRandom_Click(object sender, RoutedEventArgs e)
        {
            String len = textBoxLength.Text;
            int length = Int32.Parse(len);  //TODO try catch exceptions. Less breakable. TODO: what is the max that ps.Next() can take?? Will it throw an exception?
            setTotalPossiblePermutation(length);

            try
            {
                String encryptedPW = manager.generateNewPassword(length); 
                txtEncryptedPW.Text = encryptedPW;
                passwordChosen = true;
                isCustom = false;
                btnBeginRecov.IsEnabled = true;
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show("Error: server timed out. Please try again:\n" + ex.Message);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show("Error: could not generate random password due to server connection error:\n" + ex.Message);
            }
        }

        /// <summary>
        ///             Decreases the length of the to-be-generated password.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnLengthDown_Click(object sender, RoutedEventArgs e)
        {
            String len = textBoxLength.Text;
            int length = Int32.Parse(len);
            if(length > 0)
            {
                length--;
                textBoxLength.Text = "" + length;
                setTotalPossiblePermutation(length);
            }
            
        }

        /// <summary>
        ///             Increases the length of the to-be-generated password.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnLengthUp_Click(object sender, RoutedEventArgs e)
        {
            String len = textBoxLength.Text;
            int length = Int32.Parse(len);
            if(length < 10)
            {
                length++;
                textBoxLength.Text = "" + length;
                setTotalPossiblePermutation(length);
            }
            
        }

        /// <summary>
        ///             Sets the total possible permutations class field for the given length.
        ///             Updates the GUI as well.
        /// </summary>
        /// <param name="length"></param>
        private void setTotalPossiblePermutation(int length)
        {
            if(length > 0)
            {
                totalPossible = 1;
                for (int ii = 0; ii < length; ii++)
                {
                    totalPossible = totalPossible*(long)charSet.Length;
                }
                    //totalPossible = (long)Math.Pow((double)length, (double)charSet.Length);
                lblTotalPerms.Content = "Total Possible Permutations: " + totalPossible;
                outPlainTextLength.Content = length;
                progressBar.Maximum = totalPossible;
            }
        }
    }
}


//Olde methods when getting the stats was done Service Oriented way (And not Asynchronously).
/*private void btnBeginRecov_Click(object sender, RoutedEventArgs e)
        {
            if(passwordChosen && isCustom)
            {
                if(manager.setCustomPassword(customPassword))
                {
                    manager.begin();
                    btnBeginRecov.IsEnabled = false;
                    System.Threading.Thread diagnosticsThread;
                    diagnosticsThread = new System.Threading.Thread(new System.Threading.ThreadStart(beginDiagnostics));
                }
                else
                {
                    MessageBox.Show("Custom password contains invalid characters");
                }
               


            }
            else if(passwordChosen && !isCustom)
            {
                manager.begin();
                btnBeginRecov.IsEnabled = false;
                System.Threading.Thread diagnosticsThread = new Thread(beginDiagnostics);
                //diagnosticsThread = new System.Threading.Thread(new System.Threading.ThreadStart(beginDiagnostics));

                diagnosticsThread.Start();
            
            }
            
        }

        private void beginDiagnostics()
        {
            int numConnected = 0;   //out params for manager.getJobStats(..)
            long numTested = 0;
            long rate = 0;
            long elapsedSeconds;

            long lastNumTested = 0;;
            long lastRate = 0;

            while(!pwFound && !exit)
            {
               
                Thread.Sleep(1000); //Sleep for a second.
                
                try
                {

                    manager.getJobStats(out numConnected, out numTested, out rate, out elapsedSeconds);

                    

                    Dispatcher.Invoke((Action)delegate()
                    {
                        outNumConnected.Content = numConnected;
                        lblTotalTested.Content = "" + numTested + "/" + totalPossible;
                        outTestedPerSec.Content = "" + rate + " passwords per second";
                        

                        if(numTested > lastNumTested)
                        {
                            if (rate != 0)
                            {
                                long secToComplete = ((totalPossible - numTested) / rate);
                                TimeSpan timeLeftSpan = TimeSpan.FromSeconds(secToComplete);
                                //Took this line of code from: http://stackoverflow.com/questions/9993883/convert-milliseconds-to-human-readable-time-lapse
                                String timeLeft = string.Format("{0:D2}h:{1:D2}m:{2:D2}s", timeLeftSpan.Hours, timeLeftSpan.Minutes, timeLeftSpan.Seconds);
                                outTimeRemaining.Content = timeLeft;

                                
                            }

                            progressBar.Value = numTested;
                            double percent = (((double)numTested / (double)totalPossible) * (double)100);
                            outPercentDone.Content = "" + string.Format("{0:0.00}", percent) + "%" ;
                            lastNumTested = numTested;
                            lastRate = rate;
                        }

                        
                        


                        TimeSpan timeTakenSpan = TimeSpan.FromMilliseconds(elapsedSeconds*1000);

                        
                        //TODO: delete ms.
                        string timeTaken = string.Format("{0:D2}h:{1:D2}m:{2:D2}s",
                                                timeTakenSpan.Hours,
                                                timeTakenSpan.Minutes,
                                                timeTakenSpan.Seconds);

                        outLapsedTime.Content = timeTaken;
                    });
                    
                    //MessageBox.Show("" + numConnected + " " + numTested + " " + rate);
                    
                    
                }
                catch(Exception e)
                {
                    //MessageBox.Show("Error" + e.Message);
                }
            }

        
 * }*/



//http://www.tutorialspoint.com/wpf/wpf_controls.htm