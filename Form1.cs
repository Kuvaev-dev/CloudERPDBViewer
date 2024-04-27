using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace CloudERPDBViewer
{
    public partial class Form1 : Form
    {
        private readonly string connectionString = @"Data Source=localhost\sqlexpress;Initial Catalog=CloudErp;Integrated Security=True;";
        private string selectedColumn = ""; // ������ ��������� ������� ��� ����������
        private string selectedSortOrder = ""; // ������ ��������� ����������� ����������
        private string selectedTableName = ""; // ������ ��������� ��� ������� ��� �������, ���������� � �������� �������

        public Form1()
        {
            InitializeComponent();
            LoadSortColumns(); // �������� �������� ��� ����������
            LoadSortOrders(); // �������� ����� ����������
            LoadSortOptions(); // �������� ����� ���������� ������

            // ������������� �� ������� DataGridView ��� ����������, ������� � �������� �������
            dataGridViewData.CellDoubleClick += DataGridViewData_CellDoubleClick;
            dataGridViewData.KeyDown += DataGridViewData_KeyDown;
            dataGridViewData.RowsAdded += DataGridViewData_RowsAdded;
        }

        private void LoadSortOptions()
        {
            comboBoxSortData.Items.AddRange(new string[] {
        "Alphabetical",
        "Reverse Alphabetical",
        "First 5 Rows",
        "First 10 Rows"
    });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadDatabaseObjects();
        }

        private void LoadDatabaseObjects()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Load tables, procedures, triggers, and views
                var sql = @"
                    SELECT TABLE_NAME as Name, 'Table' as Type FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'
                    UNION
                    SELECT SPECIFIC_NAME as Name, 'Procedure' as Type FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE='PROCEDURE'
                    UNION
                    SELECT name as Name, 'Trigger' as Type FROM sys.triggers
                    UNION
                    SELECT name as Name, 'View' as Type FROM sys.views";
                var objects = connection.Query(sql);

                treeView.Nodes.Clear();

                TreeNode tablesNode = new TreeNode("Tables");
                TreeNode proceduresNode = new TreeNode("Procedures");
                TreeNode triggersNode = new TreeNode("Triggers");
                TreeNode viewsNode = new TreeNode("Views");

                foreach (var obj in objects)
                {
                    TreeNode node = new TreeNode(obj.Name.ToString());
                    node.Tag = obj.Type.ToString();

                    switch (obj.Type.ToString())
                    {
                        case "Table":
                            tablesNode.Nodes.Add(node);
                            break;
                        case "Procedure":
                            proceduresNode.Nodes.Add(node);
                            break;
                        case "Trigger":
                            triggersNode.Nodes.Add(node);
                            break;
                        case "View":
                            viewsNode.Nodes.Add(node);
                            break;
                        default:
                            break;
                    }
                }

                treeView.Nodes.Add(tablesNode);
                treeView.Nodes.Add(proceduresNode);
                treeView.Nodes.Add(triggersNode);
                treeView.Nodes.Add(viewsNode);
            }
        }

        private void LoadParameters(string procedureName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var sql = $"SELECT name, TYPE_NAME(user_type_id) AS type FROM sys.parameters WHERE object_id = OBJECT_ID('{procedureName}')";
                var parameters = connection.Query(sql);

                // ������� ���������� ���� �����
                flowLayoutPanelParameters.Controls.Clear();

                // ������� ���� ����� ��� ������� ���������
                foreach (var param in parameters)
                {
                    Label label = new Label();
                    label.Text = param.name.ToString() + ":";
                    TextBox textBox = new TextBox();
                    textBox.Name = param.name.ToString();
                    flowLayoutPanelParameters.Controls.Add(label);
                    flowLayoutPanelParameters.Controls.Add(textBox);
                }

                // ��������� ������ ��� ������ ���������
                Button executeButton = new Button();
                executeButton.Text = "������� ���������";
                executeButton.Click += (sender, e) =>
                {
                    ExecuteProcedure(procedureName);
                };
                flowLayoutPanelParameters.Controls.Add(executeButton);
            }
        }

        private void ExecuteProcedure(string procedureName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var parameters = new DynamicParameters();

                // �������� ��������� �� ��������� �����
                foreach (Control control in flowLayoutPanelParameters.Controls)
                {
                    if (control is TextBox textBox)
                    {
                        parameters.Add(textBox.Name, textBox.Text);
                    }
                }

                // �������� ��������� � �����������
                var dataTable = new DataTable();
                using (var command = new SqlCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    foreach (var parameter in parameters.ParameterNames)
                    {
                        command.Parameters.AddWithValue(parameter, parameters.Get<object>(parameter));
                    }
                    using (var adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }
                }

                // ��������� ������ ����� ��������� � DataGridView
                if (dataTable.Rows.Count > 0)
                {
                    dataGridViewData.DataSource = dataTable;
                }
                else
                {
                    MessageBox.Show("��������� �� ������� ������.");
                }
            }
        }

        private void LoadTriggerFields(string triggerName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // �������� ��� �������, � ������� �������� �������
                var sql = $"SELECT OBJECT_NAME(parent_id) AS parent_table FROM sys.triggers WHERE name = '{triggerName}'";
                var tableName = connection.QueryFirstOrDefault<string>(sql);

                if (!string.IsNullOrEmpty(tableName))
                {
                    // �������� ����� �������� �������
                    sql = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'";
                    var columns = connection.Query<string>(sql);

                    // ������� ���������� ���� �����
                    flowLayoutPanelParameters.Controls.Clear();

                    // ������� ���� ����� ��� ������� ������� �������
                    foreach (var column in columns)
                    {
                        Label label = new Label();
                        label.Text = column + ":";
                        TextBox textBox = new TextBox();
                        textBox.Name = column;
                        flowLayoutPanelParameters.Controls.Add(label);
                        flowLayoutPanelParameters.Controls.Add(textBox);
                    }

                    // ��������� ������ ��� ������ ��������
                    Button executeTriggerButton = new Button();
                    executeTriggerButton.Text = "������� �������";
                    executeTriggerButton.Click += (sender, e) =>
                    {
                        ExecuteTrigger(triggerName, tableName);
                    };
                    flowLayoutPanelParameters.Controls.Add(executeTriggerButton);
                }
                else
                {
                    MessageBox.Show("�� ������� ���������� �������, � ������� �������� �������.");
                }
            }
        }

        private void ExecuteTrigger(string triggerName, string tableName)
        {
            // ����� ����� ��������� ��������, ��������� � ������� ��������, ��������, ��������� ��������� ������ � �������
            MessageBox.Show("������� ������ �������.");
        }

        private void LoadData(string objectName)
        {
            selectedTableName = objectName; // ��������� ��� ��������� �������

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var sql = $"SELECT * FROM {objectName}"; // Initialize SQL command text

                if (!string.IsNullOrEmpty(selectedColumn)) // Check if a column is selected for sorting
                {
                    sql += $" ORDER BY {selectedColumn} {selectedSortOrder}"; // Append ORDER BY clause
                }

                var dataAdapter = new SqlDataAdapter(sql, connection);
                var dataTable = new DataTable();
                dataAdapter.Fill(dataTable);

                dataGridViewData.DataSource = dataTable;
            }
        }

        private void LoadInsertControls(string tableName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // �������� ���������� � �������� �������
                var sql = $"SELECT COLUMN_NAME, COLUMNPROPERTY(object_id(TABLE_NAME), COLUMN_NAME, 'IsIdentity') AS IsIdentity FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'";
                var columnsInfo = connection.Query(sql);

                // ������� ��������� ���� ��� ������� ������� �������, �������� ������� � ���������������
                foreach (var columnInfo in columnsInfo)
                {
                    // ���������� ������� � ���������������
                    if (columnInfo.IsIdentity == 1)
                        continue;

                    Label label = new Label();
                    label.Text = columnInfo.COLUMN_NAME + ":";
                    TextBox textBox = new TextBox();
                    textBox.Name = columnInfo.COLUMN_NAME;
                    flowLayoutPanelParameters.Controls.Add(label);
                    flowLayoutPanelParameters.Controls.Add(textBox);
                }

                // ��������� ������ ��� ������� ������
                Button addButton = new Button();
                addButton.Text = "��������";
                addButton.Click += (sender, e) =>
                {
                    InsertRecord(tableName);
                };
                flowLayoutPanelParameters.Controls.Add(addButton);
            }
        }

        private void InsertRecord(string tableName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var columns = new List<string>();
                var values = new List<string>();

                // �������� ����� �������� � �������� �� ��������� �����, ��������� ��������� ����
                foreach (Control control in flowLayoutPanelParameters.Controls)
                {
                    if (control is TextBox textBox && textBox.Name != dataGridViewData.Columns[0].HeaderText)
                    {
                        columns.Add(textBox.Name);
                        values.Add(textBox.Text);
                    }
                }

                // ��������� SQL-������ �� ������ ��������� ������
                var sql = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values.Select(v => $"'{v}'"))})";

                try
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // ��������� DataGridView ����� �������
                    LoadData(selectedTableName);
                    MessageBox.Show("������ ������� ���������.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"������ ��� ���������� ������: {ex.Message}");
                }
            }
        }

        private void DeleteRecord(string tableName, int rowIndex)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var primaryKey = dataGridViewData.Rows[rowIndex].Cells[0].Value; // �����������, ��� ������ ������� - ��� ��������� ����

                // ��������� SQL-������ �� �������� ������
                var sql = $"DELETE FROM {tableName} WHERE {dataGridViewData.Columns[0].HeaderText} = '{primaryKey}'";

                try
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // ��������� DataGridView ����� ��������
                    LoadData(selectedTableName);
                    MessageBox.Show("������ ������� �������.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"������ ��� �������� ������: {ex.Message}");
                }
            }
        }

        private void UpdateRecord(string tableName, int rowIndex, string columnName, string newValue)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var primaryKey = dataGridViewData.Rows[rowIndex].Cells[0].Value; // �����������, ��� ������ ������� - ��� ��������� ����

                // ��������� SQL-������ �� ���������� ������
                var sql = $"UPDATE {tableName} SET {columnName} = '{newValue}' WHERE {dataGridViewData.Columns[0].HeaderText} = '{primaryKey}'";

                try
                {
                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // ��������� DataGridView ����� ����������
                    LoadData(tableName);
                    MessageBox.Show("������ ������� ���������.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"������ ��� ���������� ������: {ex.Message}");
                }
            }
        }


        private void LoadSortColumns()
        {
            // ��������� ������� ��� ���������� � �����-����
            foreach (DataGridViewColumn column in dataGridViewData.Columns)
            {
                comboBoxSort.Items.Add(column.HeaderText);
            }
        }

        private void LoadSortOrders()
        {
            comboBoxSortOrder.Items.AddRange(new string[] {
                "Ascending",
                "Descending"
            });
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // �������� ������ � ������
            flowLayoutPanelParameters.Controls.Clear();

            if (e.Node.Tag != null)
            {
                string selectedType = e.Node.Tag.ToString();
                string selectedObjectName = e.Node.Text;

                if (selectedType == "Procedure")
                {
                    LoadParameters(selectedObjectName);
                }
                else if (selectedType == "Trigger")
                {
                    LoadTriggerFields(selectedObjectName);
                }
                else
                {
                    LoadData(selectedObjectName);
                    LoadInsertControls(selectedObjectName); // �������� ��������� ��� ������� ������
                    LoadSortColumns(); // �������� �������� ��� ����������
                }
            }
        }

        private void DataGridViewData_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // ��������� ������ ��� ������� ������ �� ������
            if (e.RowIndex >= 0)
            {
                DataGridViewRow selectedRow = dataGridViewData.Rows[e.RowIndex];
                string columnName = dataGridViewData.Columns[e.ColumnIndex].HeaderText;
                string newValue = selectedRow.Cells[e.ColumnIndex].Value.ToString();
                UpdateRecord(selectedTableName, e.RowIndex, columnName, newValue);
            }
        }

        private void DataGridViewData_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            // ����� ����� ���������� ������� ������
        }

        private void DataGridViewData_KeyDown(object sender, KeyEventArgs e)
        {
            // ������� ������ ��� ������� ������� Delete
            if (e.KeyCode == Keys.Delete && dataGridViewData.SelectedRows.Count > 0)
            {
                // ����� ���� ���������� ������ � ������� �
                int rowIndex = dataGridViewData.SelectedRows[0].Index;
                DeleteRecord(selectedTableName, rowIndex);
            }
        }

        private void comboBoxSort_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedColumn = comboBoxSort.SelectedItem.ToString();
            LoadData(selectedTableName);
        }

        private void comboBoxSortOrder_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedSortOrder = comboBoxSortOrder.SelectedItem.ToString() == "Ascending" ? "ASC" : "DESC";
            LoadData(selectedTableName);
        }

        private void comboBoxSortData_SelectedIndexChanged(object sender, EventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null && comboBox.SelectedItem != null)
            {
                var selectedSortOption = comboBox.SelectedItem.ToString();

                switch (selectedSortOption)
                {
                    case "Alphabetical":
                        LoadData(selectedTableName);
                        break;
                    case "Reverse Alphabetical":
                        LoadData(selectedTableName);
                        break;
                    case "First 5 Rows":
                        LoadTopNData(selectedTableName, 5);
                        break;
                    case "First 10 Rows":
                        LoadTopNData(selectedTableName, 10);
                        break;
                    default:
                        break;
                }
            }
        }

        private void LoadTopNData(string tableName, int n)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var sql = $"SELECT TOP {n} * FROM {tableName}";

                if (!string.IsNullOrEmpty(selectedColumn)) // Check if a column is selected for sorting
                {
                    sql += $" ORDER BY {selectedColumn} {selectedSortOrder}"; // Append ORDER BY clause
                }

                var dataAdapter = new SqlDataAdapter(sql, connection);
                var dataTable = new DataTable();
                dataAdapter.Fill(dataTable);

                dataGridViewData.DataSource = dataTable;
            }
        }
    }
}
