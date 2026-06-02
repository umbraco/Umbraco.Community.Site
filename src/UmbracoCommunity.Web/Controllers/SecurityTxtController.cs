using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace UmbracoCommunity.Web.Controllers;

public class SecurityTxtController : Controller
{
    public IActionResult Index()
    {
        var builder = new StringBuilder();

        builder.AppendLine("Contact: mailto:security@umbraco.com");
        builder.AppendLine("Expires: 2030-12-30T23:00:00.000Z");
        builder.AppendLine("Acknowledgments: https://umbraco.com/trust-center/security-and-umbraco/how-to-report-a-vulnerability-in-umbraco/list-of-security-contributors/");
        builder.AppendLine("Preferred-Languages: en, da");
        builder.AppendLine("Policy: https://umbraco.com/trust-center/security-and-umbraco/how-to-report-a-vulnerability-in-umbraco/");
        builder.AppendLine("Hiring: https://umbraco.com/work-at-umbraco/job-openings/");

        return Content(builder.ToString(), "text/plain");
    }
}
