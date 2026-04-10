using System;
using System.Collections.Generic;

namespace LegacyRenewalApp
{
    // --- BRAMKA PŁATNOŚCI (ADAPTER) ---
    public interface IBillingGateway
    {
        void SaveInvoice(RenewalInvoice invoice);
        void SendEmail(string email, string subject, string body);
    }

    public class BillingGatewayAdapter : IBillingGateway
    {
        public void SaveInvoice(RenewalInvoice invoice) => LegacyBillingGateway.SaveInvoice(invoice);
        public void SendEmail(string email, string subject, string body) => LegacyBillingGateway.SendEmail(email, subject, body);
    }

    // --- REPOZYTORIA ---
    public interface ICustomerRepository
    {
        Customer GetById(int customerId);
    }

    public interface ISubscriptionPlanRepository
    {
        SubscriptionPlan GetByCode(string code);
    }
}
