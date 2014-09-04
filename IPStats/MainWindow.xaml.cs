using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SoftFluent.Tools;

namespace IPStats
{
    public partial class MainWindow : Window
    {
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        private GridViewColumnHeader _lastHeaderClicked;

        public MainWindow()
        {
            InitializeComponent();
            IList<TcpConnection> all = TcpConnection.GetAll(true);
            Connections.ItemsSource = all;
            Title = "TCP connections - " + all.Count;
        }

        private class IPEndPointComparer : IComparer
        {
            private ListSortDirection _direction;
            private bool _remote;

            public IPEndPointComparer(bool remote, ListSortDirection direction)
            {
                _remote = remote;
                _direction = direction;
            }

            public int Compare(object x, object y)
            {
                if (ReferenceEquals(x, y))
                    return 0;

                TcpConnection cx = (TcpConnection)x;
                TcpConnection cy = (TcpConnection)y;
                IPEndPoint px = _remote ? cx.RemoteEndPoint : cx.LocalEndPoint;
                IPEndPoint py = _remote ? cy.RemoteEndPoint : cy.LocalEndPoint;
                
                int result;
                if (px.Address.Equals(py.Address))
                {
                    result = px.Port.CompareTo(py.Port);
                }
                else
                {
                    result = px.Address.ToString().CompareTo(py.Address.ToString());
                }
                return _direction == ListSortDirection.Ascending ? result : -result;
            }
        }

        private class IPAddressComparer : IComparer
        {
            private ListSortDirection _direction;
            private bool _remote;

            public IPAddressComparer(bool remote, ListSortDirection direction)
            {
                _remote = remote;
                _direction = direction;
            }

            public int Compare(object x, object y)
            {
                if (ReferenceEquals(x, y))
                    return 0;

                TcpConnection cx = (TcpConnection)x;
                TcpConnection cy = (TcpConnection)y;
                IPEndPoint px = _remote ? cx.RemoteEndPoint : cx.LocalEndPoint;
                IPEndPoint py = _remote ? cy.RemoteEndPoint : cy.LocalEndPoint;

                int result = px.Address.ToString().CompareTo(py.Address.ToString());
                return _direction == ListSortDirection.Ascending ? result : -result;
            }
        }

        private void Sort(ListView listview, string sortBy, ListSortDirection direction)
        {
            ListCollectionView defaultView = (ListCollectionView)CollectionViewSource.GetDefaultView(listview.ItemsSource);
            SortDescription item = new SortDescription(sortBy, direction);
            if (sortBy == "LocalEndPoint" || sortBy == "RemoteEndPoint")
            {
                using (defaultView.DeferRefresh())
                {
                    defaultView.SortDescriptions.Clear();
                    defaultView.SortDescriptions.Add(item);
                    defaultView.CustomSort = new IPEndPointComparer(sortBy == "RemoteEndPoint", direction);
                }
                return;
            }

            if (sortBy == "LocalEndPoint.Address" || sortBy == "RemoteEndPoint.Address")
            {
                using (defaultView.DeferRefresh())
                {
                    defaultView.SortDescriptions.Clear();
                    defaultView.SortDescriptions.Add(item);
                    defaultView.CustomSort = new IPAddressComparer(sortBy == "RemoteEndPoint.Address", direction);
                }
                return;
            }

            defaultView.CustomSort = null;
            defaultView.SortDescriptions.Clear();
            defaultView.SortDescriptions.Add(item);
            defaultView.Refresh();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled)
            {
                MainMenu.RaiseMenuItemClickOnKeyGesture(e);
            }
        }

        private void Connections_ColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader originalSource = e.OriginalSource as GridViewColumnHeader;
            if (originalSource == null || originalSource.Role == GridViewColumnHeaderRole.Padding)
                return;

            ListSortDirection ascending;
            if (originalSource != _lastHeaderClicked)
            {
                ascending = ListSortDirection.Ascending;
            }
            else
            {
                ascending = _lastDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            }

            string path = ((Binding)originalSource.Column.DisplayMemberBinding).Path.Path;
            Sort((ListView)sender, path, ascending);
            _lastHeaderClicked = originalSource;
            _lastDirection = ascending;
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuRefresh_Click(object sender, RoutedEventArgs e)
        {
            IList<TcpConnection> itemsSource = (IList<TcpConnection>)this.Connections.ItemsSource;
            TcpConnection.Update(itemsSource);
            Title = "TCP connections - " + itemsSource.Count;
        }
    }
}
