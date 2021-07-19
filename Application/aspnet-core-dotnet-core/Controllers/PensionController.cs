﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pension_Management_Portal.Models;
using Pension_Management_Portal.Repository;

namespace Pension_Management_Portal.Controllers
{
    public class PensionController : Controller
    {
        static string token;
        private readonly ILogger<PensionController> _logger;
        private IConfiguration configuration;
        static readonly log4net.ILog _log4net = log4net.LogManager.GetLogger(typeof(PensionController));
        PensionDetail penDetailObj = new PensionDetail();
        PensionDetail ResDetails = new PensionDetail();
        private readonly IPensionPortalRepo repo;
        public PensionController(ILogger<PensionController> logger,IConfiguration _configuration, IPensionPortalRepo _repo)
        {
            _logger = logger;
            configuration = _configuration;
            repo = _repo;
        }
        /// <summary>
        /// Login form displayed to user
        /// </summary>
        /// <returns></returns>
        public ActionResult Login()
        {
            _log4net.Info("Pensioner is logging in");
            return View();
        }

        /// <summary>
        /// Taking the login credentials and passing it to authorization api to get the token
        /// </summary>
        /// <param name="cred"></param>
        /// <returns></returns>
      
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(Login cred)
        {
            _log4net.Info("Post Login is called");
            Login loginCred = new Login();
            string tokenValue = configuration.GetValue<string>("MyLinkValue:tokenUri");

            using (var httpClient = new HttpClient())
            {
                StringContent content = new StringContent(JsonConvert.SerializeObject(cred), Encoding.UTF8, "application/json");

                using (var response = await httpClient.PostAsync("https://localhost:44365/api/Auth/", content))
                {

                    if (!response.IsSuccessStatusCode)
                    {
                        _log4net.Info("Login failed");
                        ViewBag.Message = "Please Enter valid credentials";
                        return View("Login");
                    }
                    _log4net.Info("Login Successful and token generated");
                    string strtoken = await response.Content.ReadAsStringAsync();


                    loginCred = JsonConvert.DeserializeObject<Login>(strtoken);
                    string userName = cred.Username;
                    token = strtoken;
                    HttpContext.Session.SetString("token", strtoken);
                    HttpContext.Session.SetString("user", JsonConvert.SerializeObject(cred));
                    HttpContext.Session.SetString("owner", userName);
                }
            }
            return View("PensionPortal");
        }

       

        /// <summary>
        /// For logging out of the current session
        /// </summary>
        /// <returns></returns>
        public ActionResult Logout()
        {
            HttpContext.Session.Clear();
            
            return View("Login");
        }

        /// <summary>
        /// Getting the input values from the pensioner
        /// </summary>
        /// <returns></returns>
        public ActionResult PensionPortal()
        {
           
            if (HttpContext.Session.GetString("token") == null)
            {
                _log4net.Info("Pensioner is not logged in");
                ViewBag.Message = "Please Login First";
                return View("Login");
            }
            _log4net.Info("Pensioner is entering his details");
            return View();
        }

        /// <summary>
        /// processing the Input Values 
        /// </summary>
        /// <param name="input"></param>
        /// <returns> Output View </returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> PensionPortal(PensionerInput input)
        {
            if (HttpContext.Session.GetString("token") == null)
            {
                _log4net.Info("Pensioner is not logged in");
                ViewBag.Message = "Please Login First";
                return View("Login");
            }

            _log4net.Info("Processing the pension began");

            
            string processValue = configuration.GetValue<string>("MyLinkValue:processUri");

            if (ModelState.IsValid)
            {
                using (var client = new HttpClient())
                {
                    
                    StringContent content = new StringContent(JsonConvert.SerializeObject(input), Encoding.UTF8, "application/json");
                    client.BaseAddress = new Uri(processValue);                   

                    try
                    {
                        using (var response = await client.PostAsync("https://localhost:44394/api/ProcessPension/ProcessPension/", content))
                        {
                            var apiResponse = await response.Content.ReadAsStringAsync();
                            ProcessResponse res = JsonConvert.DeserializeObject<ProcessResponse>(apiResponse);
                            penDetailObj.Status = res.Result.Status;
                            penDetailObj.PensionAmount = res.Result.PensionAmount;

                            ResDetails.PensionAmount = penDetailObj.PensionAmount;
                            ResDetails.Status = penDetailObj.Status;
      
                        }
                    }
                    catch (Exception e)
                    {
                        _log4net.Error("Some Microservice is Down!!");
                        penDetailObj = null;
                    }
                }

                if (penDetailObj == null)
                {
                    _log4net.Error("Some Microservice is Down!!");
                    ViewBag.erroroccured = "Some Error Occured";
                    return View();
                }
                if (penDetailObj.Status.Equals(21))
                {
                    _log4net.Error("Some Microservice is Down!!");
                    ViewBag.erroroccured = "Some Error Occured";
                    return View();
                }
                if (penDetailObj.Status.Equals(10))
                {
                    // Storing the Values in Database
                    _log4net.Info("Pensioner details have been matched with the Csv and data is successfully saved in local Database!!");
                    repo.AddResponse(ResDetails);
                    repo.Save();
                    return RedirectToAction("PensionervaluesDisplayed", ResDetails);
                }
                else
                {
                    _log4net.Error("Persioner details does not match with the Csv!!");
                    ViewBag.notmatch = "Pensioner Values not match";
                    return View();
                }
            }
            _log4net.Warn("Proper details are not given by the Admin!!");
            ViewBag.invalid = "Pensioner Values are Invalid";
            return View();            
        }

        public ActionResult PensionervaluesDisplayed(PensionDetail detail)
        {
            return View(detail);
        }






    }
}
