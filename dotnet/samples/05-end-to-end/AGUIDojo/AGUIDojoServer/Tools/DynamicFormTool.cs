using System.ComponentModel;

namespace AGUIDojoServer.Tools;

/// <summary>
/// AI tool that generates dynamic form definitions for the <c>show_form</c> generative UI pattern.
/// </summary>
/// <remarks>
/// This tool demonstrates the tool-based generative UI pattern where the AI calls a tool
/// whose result is rendered as a rich UI component (a dynamic form) on the client side.
/// The client's <c>DynamicFormDisplay</c> component renders the returned <see cref="DynamicFormResult"/>.
/// </remarks>
internal static class DynamicFormTool
{
    /// <summary>
    /// The simulated delay in milliseconds to demonstrate tool call progress in the UI.
    /// </summary>
    private const int SimulatedDelayMs = 1000;

    /// <summary>
    /// Generates a dynamic form definition based on the requested form type.
    /// </summary>
    /// <param name="formType">The type of form to generate (e.g., "contact", "feedback", "registration", "order").</param>
    /// <returns>A <see cref="DynamicFormResult"/> containing the form title, description, and field definitions.</returns>
    [Description("Show a dynamic form for user input. Use this when the user needs to fill out structured information like contact details, feedback, registrations, or orders.")]
    public static async Task<DynamicFormResult> ShowFormAsync(
        [Description("The type of form to display (e.g., 'contact', 'feedback', 'registration', 'order').")] string formType)
    {
        // Add artificial delay to demonstrate tool call progress in the UI
        await Task.Delay(SimulatedDelayMs);

        return formType.ToUpperInvariant() switch
        {
            var t when t.Contains("CONTACT", StringComparison.Ordinal) => GenerateContactForm(),
            var t when t.Contains("FEEDBACK", StringComparison.Ordinal) || t.Contains("REVIEW", StringComparison.Ordinal) => GenerateFeedbackForm(),
            var t when t.Contains("REGISTER", StringComparison.Ordinal) || t.Contains("SIGNUP", StringComparison.Ordinal) => GenerateRegistrationForm(),
            var t when t.Contains("ORDER", StringComparison.Ordinal) || t.Contains("PURCHASE", StringComparison.Ordinal) => GenerateOrderForm(),
            _ => GenerateContactForm() // Default to contact form
        };
    }

    private static DynamicFormResult GenerateContactForm()
    {
        return new DynamicFormResult(
            Title: "Contact Us",
            Description: "Fill out the form below and we'll get back to you within 24 hours.",
            Fields:
            [
                new FormFieldDefinition("name", "Full Name", "text", Placeholder: "Enter your full name", Required: true),
                new FormFieldDefinition("email", "Email Address", "email", Placeholder: "you@example.com", Required: true),
                new FormFieldDefinition("phone", "Phone Number", "text", Placeholder: "+1 (555) 000-0000"),
                new FormFieldDefinition("subject", "Subject", "select", Required: true, Options: ["General Inquiry", "Technical Support", "Billing", "Partnership", "Other"]),
                new FormFieldDefinition("message", "Message", "textarea", Placeholder: "How can we help you?", Required: true),
            ],
            SubmitLabel: "Send Message");
    }

    private static DynamicFormResult GenerateFeedbackForm()
    {
        return new DynamicFormResult(
            Title: "Share Your Feedback",
            Description: "We value your opinion! Let us know how we can improve.",
            Fields:
            [
                new FormFieldDefinition("name", "Your Name", "text", Placeholder: "Enter your name"),
                new FormFieldDefinition("rating", "Overall Rating", "select", Required: true, Options: ["5 - Excellent", "4 - Good", "3 - Average", "2 - Poor", "1 - Very Poor"]),
                new FormFieldDefinition("category", "Feedback Category", "select", Required: true, Options: ["Product Quality", "Customer Service", "User Experience", "Performance", "Documentation"]),
                new FormFieldDefinition("recommend", "Would you recommend us?", "checkbox", DefaultValue: "true"),
                new FormFieldDefinition("comments", "Additional Comments", "textarea", Placeholder: "Tell us more about your experience..."),
            ],
            SubmitLabel: "Submit Feedback");
    }

    private static DynamicFormResult GenerateRegistrationForm()
    {
        return new DynamicFormResult(
            Title: "Create Account",
            Description: "Join our platform to get started.",
            Fields:
            [
                new FormFieldDefinition("firstName", "First Name", "text", Placeholder: "John", Required: true),
                new FormFieldDefinition("lastName", "Last Name", "text", Placeholder: "Doe", Required: true),
                new FormFieldDefinition("email", "Email Address", "email", Placeholder: "john.doe@example.com", Required: true),
                new FormFieldDefinition("company", "Company", "text", Placeholder: "Acme Corp"),
                new FormFieldDefinition("role", "Role", "select", Options: ["Developer", "Designer", "Product Manager", "Data Scientist", "Executive", "Other"]),
                new FormFieldDefinition("newsletter", "Subscribe to newsletter", "checkbox", DefaultValue: "true"),
                new FormFieldDefinition("terms", "I agree to the Terms of Service", "checkbox", Required: true),
            ],
            SubmitLabel: "Create Account");
    }

    private static DynamicFormResult GenerateOrderForm()
    {
        return new DynamicFormResult(
            Title: "Place Order",
            Description: "Complete the details below to place your order.",
            Fields:
            [
                new FormFieldDefinition("product", "Product", "select", Required: true, Options: ["Starter Plan - $9/mo", "Professional Plan - $29/mo", "Enterprise Plan - $99/mo", "Custom Plan"]),
                new FormFieldDefinition("quantity", "Quantity", "number", Placeholder: "1", Required: true, DefaultValue: "1"),
                new FormFieldDefinition("billingName", "Billing Name", "text", Placeholder: "Full name on card", Required: true),
                new FormFieldDefinition("billingEmail", "Billing Email", "email", Placeholder: "billing@example.com", Required: true),
                new FormFieldDefinition("coupon", "Coupon Code", "text", Placeholder: "Enter coupon code"),
                new FormFieldDefinition("notes", "Order Notes", "textarea", Placeholder: "Any special instructions?"),
            ],
            SubmitLabel: "Place Order");
    }
}
