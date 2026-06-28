using Xunit;
using System.Collections.Generic;
using Telesale.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Models;
using System.Threading.Tasks;
using System;

namespace Telesale.Api.Tests;

public class StatusPolicyTests
{
    [Fact]
    public void TestCanonicalStatusSets()
    {
        // Customer
        Assert.True(StatusPolicy.IsValidCustomerStatus("New"));
        Assert.False(StatusPolicy.IsValidCustomerStatus("Assigned"));
        Assert.False(StatusPolicy.IsValidCustomerStatus("Booking"));
        Assert.True(StatusPolicy.IsValidCustomerStatus("Wait"));
        Assert.True(StatusPolicy.IsValidCustomerStatus("Sent"));
        Assert.True(StatusPolicy.IsValidCustomerStatus("Win"));
        Assert.True(StatusPolicy.IsValidCustomerStatus("Lost"));
        Assert.False(StatusPolicy.IsValidCustomerStatus("REPLACE"));
        Assert.False(StatusPolicy.IsValidCustomerStatus("InvalidStatus"));

        // Device
        Assert.True(StatusPolicy.IsValidDeviceStatus("New"));
        Assert.False(StatusPolicy.IsValidDeviceStatus("Booking"));
        Assert.True(StatusPolicy.IsValidDeviceStatus("Win"));
        Assert.True(StatusPolicy.IsValidDeviceStatus("Lost"));
        Assert.False(StatusPolicy.IsValidDeviceStatus("Discuss"));

        // Project
        Assert.True(StatusPolicy.IsValidProjectStatus("Discuss"));
        Assert.True(StatusPolicy.IsValidProjectStatus("Quotation"));
        Assert.True(StatusPolicy.IsValidProjectStatus("Win"));
        Assert.True(StatusPolicy.IsValidProjectStatus("Lost"));
        Assert.True(StatusPolicy.IsValidProjectStatus("Hold"));
        Assert.True(StatusPolicy.IsValidProjectStatus("Cancel"));
        Assert.False(StatusPolicy.IsValidProjectStatus("New"));
    }

    [Fact]
    public void TestGetInvalidStatusMessage()
    {
        var msg = StatusPolicy.GetInvalidStatusMessage("Project", "New", StatusPolicy.ProjectStatuses);
        Assert.Equal("Invalid project status 'New'. Allowed values: Discuss, Quotation, Win, Lost, Hold, Cancel.", msg);
    }
}
