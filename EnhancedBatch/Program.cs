using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;

namespace EnhancedBatch
{
    public class Program
    {
        public static async Task Main()
        {
            /* Configuration Values */
            MyConfig configuration = InitializeConfig();

            /* Do the auth stuff first */
            IPublicClientApplication publicClientApplication = PublicClientApplicationBuilder
                .Create(configuration.ClientId).WithRedirectUri("http://localhost:1234")
                .Build();
            InteractiveAuthenticationProvider authenticationProvider = new InteractiveAuthenticationProvider(publicClientApplication, configuration.Scopes);

            /* Get the client */
            GraphServiceClient graphClient = new GraphServiceClient(authenticationProvider);

            /* Get a valid token in cache */
            await AcquireTokenToCache(graphClient);

            /* Create a HttpQuery for use */
            HttpQuery query = new HttpQuery(graphClient);

            /* Run the four versions */
            await Run0(graphClient);
            await Run1(query, graphClient);
            await Run2(query, graphClient);
            Run3(graphClient);
        }

        /// <summary>
        /// Run the request in the normal fashion.
        /// </summary>
        /// <param name="graphClient"></param>
        /// <returns></returns>
        public static async Task Run0(GraphServiceClient graphClient)
        {
            /* Request version 0 */
            /* Uses the normal way type */
            User user = await graphClient.Me.Request().GetAsync();
            Calendar calendar = await graphClient.Me.Calendar.Request().GetAsync();
            Drive drive = await graphClient.Me.Drive.Request().GetAsync();

            Console.WriteLine("Version 0 : Normal async/await fashion");
            Console.WriteLine("Display Name user: " + user.DisplayName);
            Console.WriteLine("Calendar Owner Address: " + calendar.Owner.Address);
            Console.WriteLine("Display Drive Type: " + drive.DriveType);
            Console.WriteLine("\r\n\r\n");
        }

        /// <summary>
        /// Run the request in the normal fashion.
        /// </summary>
        /// <param name="graphClient"></param>
        /// <returns></returns>
        public static void Run00(GraphServiceClient graphClient)
        {
            User user = null;
            Calendar calendar = null;
            Drive drive = null;
            Task[] tasks = new Task[3];

            /* Request version 0 part one*/
            /* Uses the normal way with user created tasks */
            //user task
            tasks[0] = graphClient.Me.Request().GetAsync()
                .ContinueWith(t => {
                    if (t.IsCompleted)
                    {
                        user = t.Result;
                    }
                });

            //calendar task
            tasks[1] = graphClient.Me.Calendar.Request().GetAsync()
                .ContinueWith(t => {
                    if (t.IsCompleted)
                    {
                        calendar = t.Result;
                    }
                });

            //drive task
            tasks[2] = graphClient.Me.Drive.Request().GetAsync()
                .ContinueWith(t => {
                    if (t.IsCompleted)
                    {
                        drive = t.Result;
                    }
                });

            //wait for all tasks
            Task.WaitAll(tasks);

            //Print out the results
            Console.WriteLine("Version 0 : Normal async/await fashion");
            Console.WriteLine("Display Name user: " + user.DisplayName);
            Console.WriteLine("Calendar Owner Address: " + calendar.Owner.Address);
            Console.WriteLine("Display Drive Type: " + drive.DriveType);
            Console.WriteLine("\r\n\r\n");
        }

        /// <summary>
        /// Use the HttpQuery class to add requests and then execute them.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="graphClient"></param>
        /// <returns></returns>
        private static async Task Run1(HttpQuery query, GraphServiceClient graphClient)
        {
            /* Request version 1 */
            /* Parallel Requests Task */
            ViewModel model = new ViewModel();
            query.AddRequest<User>(graphClient.Me.Request(), u => model.Me = u);
            query.AddRequest<Calendar>(graphClient.Me.Calendar.Request(), cal => model.Calendar = cal);
            query.AddRequest<Drive>(graphClient.Me.Drive.Request(), dr => model.Drive = dr);

            await query.ExecuteAsync();//run them at the same time :)
            Console.WriteLine("Version 1 : AddRequest in typed fashion");
            Console.WriteLine("Display Name user: " + model.Me.DisplayName);
            Console.WriteLine("Display Owner Address: " + model.Calendar.Owner.Address);
            Console.WriteLine("Display Drive Type: " + model.Drive.DriveType);
            Console.WriteLine("\r\n\r\n");
        }

        /// <summary>
        /// Use the HttpQuery Class to populate a dynamic type to use.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="graphClient"></param>
        /// <returns></returns>
        private static async Task Run2(HttpQuery query, GraphServiceClient graphClient)
        {
            /* Request version 2 */
            /* Composed Response */
            /* Uses the dynamic type */
            dynamic result = await query.PopulateAsync(new
            {
                Me = graphClient.Me.Request(),
                Calendar = graphClient.Me.Calendar.Request(),
                Drive = graphClient.Me.Drive.Request()
            });

            Console.WriteLine("Version 2 : PopulateAsync with dynamic type");
            Console.WriteLine("Display Name user: " + result.Me.displayName);
            Console.WriteLine("Calendar Owner Address: " + result.Calendar.owner.address);
            Console.WriteLine("Display Drive Type: " + result.Drive.driveType);
            Console.WriteLine("\r\n\r\n");
        }

        /// <summary>
        /// Use a response handler to launch a fire and forget fashioned call.
        /// </summary>
        /// <param name="graphClient"></param>
        /// <returns></returns>
        private static void Run3(GraphServiceClient graphClient)
        {
            /* Request version 3 */
            /* Declared Response */
            ViewModel viewModel = new ViewModel();
            //register an event handler for the model
            viewModel.PropertyChanged += ModelPropertyChanged;
            ResponseHandler responseHandler = new ResponseHandler();
            responseHandler.OnSuccess<User>(u => viewModel.Me = u);
            responseHandler.OnSuccess<Calendar>(cal => viewModel.Calendar = cal);
            responseHandler.OnSuccess<Drive>(dr => viewModel.Drive = dr);
            responseHandler.OnClientError(e => Console.WriteLine(e.Message));
            responseHandler.OnServerError(e => Console.WriteLine(e.Message));

            graphClient.Me.Request().SendGet(responseHandler);
            graphClient.Me.Calendar.Request().SendGet(responseHandler);
            graphClient.Me.Drive.Request().SendGet(responseHandler);

            Console.WriteLine("Version 3 : Fire and Forget with response handler");
            Console.WriteLine("Requests Fired Away. Awaiting responses :)");
            Console.ReadKey();//wait for the responses
            Console.ReadKey();//wait for the responses
        }

        /// <summary>
        /// Event handler for the ViewModel class to display certain properties
        /// and elapsed time on the console
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void ModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is ViewModel model)
            {
                switch (e.PropertyName)
                {
                    case nameof(ViewModel.Drive):
                        Console.WriteLine("Display Drive Type: " + model.Drive.DriveType);
                        break;
                    case nameof(ViewModel.Me):
                        Console.WriteLine("Display Name user: " + model.Me.DisplayName);
                        break;
                    case nameof(ViewModel.Calendar):
                        Console.WriteLine("Calendar Owner Address: " + model.Calendar.Owner.Address);
                        break;
                }

                //check if everything has been populated so that we can display results.
                if (null != model.Drive && null != model.Calendar && null != model.Me)
                {
                    Console.WriteLine("\r\n\r\n");
                }
            }
        }

        /// <summary>
        /// This is a barrier synchronization mechanism/hack to acquire the token to have in cache
        /// so that requests being sent out in parallel by this instance do not necessarily have to spend time fetching tokens
        /// and use the local copy instead.
        /// </summary>
        /// <returns></returns>
        private static async Task AcquireTokenToCache(GraphServiceClient graphClient)
        {
            //Just authenticate a dummy message but no need to send it out coz we just need a valid token in the cache
            HttpRequestMessage dummyRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/");
            await graphClient.AuthenticationProvider.AuthenticateRequestAsync(dummyRequestMessage);
        }

        /// <summary>
        /// Read from the relevant configuration file the configs we need
        /// </summary>
        /// <returns>valid configuration</returns>
        private static MyConfig InitializeConfig()
        {
            MyConfig myConfig = new MyConfig();
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appSettings.json", false, true)
                .Build();
            myConfig.ClientId = config["clientId"];
            myConfig.Scopes = config.GetSection("scopes").GetChildren().Select(x => x.Value).ToArray();
            return myConfig;
        }
    }

    public class MyConfig
    {
        public string ClientId { get; set; }
        public IEnumerable<string> Scopes { get; set; }
    }
}
