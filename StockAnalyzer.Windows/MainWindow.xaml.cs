﻿using StockAnalyzer.Core;
using StockAnalyzer.Core.Domain;
using StockAnalyzer.Core.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace StockAnalyzer.Windows;

public partial class MainWindow : Window
{
    private static string API_URL = "https://ps-async.fekberg.com/api/stocks";
    private Stopwatch stopwatch = new Stopwatch();

    public MainWindow()
    {
        InitializeComponent();
    }

    CancellationTokenSource? cancellationTokenSource;

    private async void Search_Click(object sender, RoutedEventArgs e)
    {

        if (cancellationTokenSource is not null)
        {
            //Already hace an instance of the cancellation token source?
            //This means the button has already been pressed!
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;

            Search.Content = "Search";
            return;
        }

        try
        {
            cancellationTokenSource = new();

            Search.Content = "Cancel";

            BeforeLoadingStockData();
            var service = new StockService();

            var identifiers = StockIdentifier.Text.Split(',', ' ');

            var loadingTasks = new List<Task<IEnumerable<StockPrice>>>();
            var stocks = new ConcurrentBag<StockPrice>();

            foreach (var identifier in identifiers)
            {
                var loadTask = service.GetStockPricesFor(identifier, cancellationTokenSource.Token);


                loadTask = loadTask.ContinueWith(t =>
                {
                    var aFewStocks = t.Result.Take(5);

                    foreach (var stock in aFewStocks)
                    {
                        stocks.Add(stock);
                    }

                    Dispatcher.Invoke(() =>
                    {
                           Stocks.ItemsSource = stocks.ToArray();
                    });

                    return aFewStocks;
                });

                loadingTasks.Add(loadTask);
            }

            var timeout = Task.Delay(10000);

            var allStocksLoadingTask = Task.WhenAll(loadingTasks);

            var completedTask = await Task.WhenAny(timeout, allStocksLoadingTask);

            if (completedTask == timeout)
            {
                cancellationTokenSource.Cancel();
                throw new OperationCanceledException("Timeout!");
            }


        }
        catch (Exception ex)
        {
            Notes.Text = ex.Message;
        }
        finally
        {
            AfterLoadingStockData();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
            Search.Content = "Search";
        }
    }

    private static Task<string[]> SearchForStocks(CancellationToken cancellationToken)
    {


        return Task.Run(() =>
        {
            var lines = File.ReadAllLines(@"StockPrices_Small.csv");

            return lines;
        }, cancellationToken);
    }

    private async Task GetStocks()
    {
        try
        {
            var store = new DataStore();

            var responseTask = store.GetStockPrices(StockIdentifier.Text);

            Stocks.ItemsSource = await responseTask;
        }
        catch (Exception ex)
        {
            Notes.Text = ex.Message;
        }
    }


    private void BeforeLoadingStockData()
    {
        stopwatch.Restart();
        StockProgress.Visibility = Visibility.Visible;
        StockProgress.IsIndeterminate = true;
    }

    private void AfterLoadingStockData()
    {
        StocksStatus.Text = $"Loaded stocks for {StockIdentifier.Text} in {stopwatch.ElapsedMilliseconds}ms";
        StockProgress.Visibility = Visibility.Hidden;
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });

        e.Handled = true;
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}