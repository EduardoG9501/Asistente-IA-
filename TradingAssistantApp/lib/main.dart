import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;

void main() {
  runApp(const TradingAssistantApp());
}

class TradingAssistantApp extends StatelessWidget {
  const TradingAssistantApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'TradingAssistant Client',
      debugShowCheckedModeBanner: false,
      theme: ThemeData.dark().copyWith(
        scaffoldBackgroundColor: const Color(0xFF0F172A),
        colorScheme: const ColorScheme.dark(
          primary: Colors.indigoAccent,
          secondary: Colors.purpleAccent,
          surface: Color(0xFF1E293B),
        ),
        cardTheme: const CardTheme(
          color: Color(0xFF1E293B),
          elevation: 4,
          margin: EdgeInsets.symmetric(vertical: 8, horizontal: 16),
        ),
      ),
      home: const DashboardScreen(),
    );
  }
}

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({super.key});

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  // IP por defecto del servidor local en el mismo wifi
  String _serverIp = '192.168.1.100'; 
  String _serverPort = '5053';
  
  List<dynamic> _signals = [];
  bool _isLoading = false;
  String _statusMessage = 'Desconectado';
  Color _statusColor = Colors.redAccent;
  
  // Parámetros de la cuenta
  double _capital = 200.0;
  double _risk = 1.0;
  bool _botEnabled = true;

  @override
  void initState() {
    super.initState();
    _refreshData();
  }

  String get _baseUrl => 'http://$_serverIp:$_serverPort';

  Future<void> _refreshData() async {
    setState(() {
      _isLoading = true;
    });

    try {
      // 1. Intentar cargar configuraciones
      final settingsRes = await http.get(Uri.parse('$_baseUrl/api/trading/settings')).timeout(const Duration(seconds: 4));
      if (settingsRes.statusCode == 200) {
        final settings = json.decode(settingsRes.body);
        _capital = (settings['capital'] as num).toDouble();
        _risk = (settings['maxRiskPercentage'] as num).toDouble();
        _botEnabled = settings['botEnabled'] as bool;
        _statusMessage = 'Conectado';
        _statusColor = Colors.greenAccent;
      }

      // 2. Intentar cargar señales
      final signalsRes = await http.get(Uri.parse('$_baseUrl/api/trading/signals?limit=20')).timeout(const Duration(seconds: 4));
      if (signalsRes.statusCode == 200) {
        setState(() {
          _signals = json.decode(signalsRes.body);
        });
      }
    } catch (e) {
      setState(() {
        _statusMessage = 'Error de conexión';
        _statusColor = Colors.amberAccent;
      });
      _showSnackbar('No se pudo conectar a $_baseUrl. Revisa la IP del servidor.');
    } finally {
      setState(() {
        _isLoading = false;
      });
    }
  }

  Future<void> _updateSettings() async {
    try {
      final res = await http.post(
        Uri.parse('$_baseUrl/api/trading/settings'),
        headers: {'Content-Type': 'application/json'},
        body: json.encode({
          'capital': _capital,
          'maxRiskPercentage': _risk,
          'botEnabled': _botEnabled,
        }),
      );

      if (res.statusCode == 200) {
        _showSnackbar('Ajustes guardados correctamente.');
        _refreshData();
      }
    } catch (e) {
      _showSnackbar('Error al guardar ajustes.');
    }
  }

  void _copyToClipboard(String label, String value) {
    Clipboard.setData(ClipboardData(text: value));
    _showSnackbar('$label copiado al portapapeles: $value');
  }

  void _showSnackbar(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        duration: const Duration(seconds: 2),
        backgroundColor: Colors.indigo,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  // Helper para dar formato legible de hora local de cada usuario
  String _formatToLocalTime(String utcDateString) {
    try {
      String formattedStr = utcDateString;
      if (formattedStr.contains('.')) {
        formattedStr = formattedStr.split('.')[0];
      }
      if (!formattedStr.endsWith('Z') && !formattedStr.contains('+') && !formattedStr.contains('-')) {
        formattedStr += 'Z';
      }
      final utcDate = DateTime.parse(formattedStr);
      final localDate = utcDate.toLocal();
      final period = localDate.hour >= 12 ? 'p. m.' : 'a. m.';
      final displayHour = localDate.hour == 0 
          ? 12 
          : (localDate.hour > 12 ? localDate.hour - 12 : localDate.hour);
      
      return '${displayHour.toString().padLeft(2, '0')}:${localDate.minute.toString().padLeft(2, '0')}:${localDate.second.toString().padLeft(2, '0')} $period';
    } catch (e) {
      return '-';
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('TradingAssistant Mobile'),
        backgroundColor: const Color(0xFF1E293B),
        actions: [
          IconButton(
            onPressed: _refreshData,
            icon: _isLoading 
              ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
              : const Icon(Icons.refresh),
          ),
        ],
      ),
      body: Column(
        children: [
          // Conexión y Estado del Servidor
          _buildConnectionPanel(),
          
          // Listado de Señales
          Expanded(
            child: _isLoading && _signals.isEmpty
              ? const Center(child: CircularProgressIndicator())
              : _signals.isEmpty
                ? const Center(child: Text('No hay señales registradas aún.', style: TextStyle(color: Colors.grey)))
                : RefreshIndicator(
                    onRefresh: _refreshData,
                    child: ListView.builder(
                      itemCount: _signals.length,
                      itemBuilder: (context, index) {
                        final signal = _signals[index];
                        return _buildSignalCard(signal);
                      },
                    ),
                  ),
          ),
        ],
      ),
    );
  }

  Widget _buildConnectionPanel() {
    return Container(
      color: const Color(0xFF1E293B),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      child: Column(
        children: [
          Row(
            children: [
              Expanded(
                child: TextField(
                  decoration: const InputDecoration(
                    labelText: 'IP del Servidor Backend',
                    isDense: true,
                    contentPadding: EdgeInsets.symmetric(vertical: 8, horizontal: 8),
                    border: OutlineInputBorder(),
                  ),
                  controller: TextEditingController(text: _serverIp),
                  onChanged: (val) => _serverIp = val,
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: TextField(
                  decoration: const InputDecoration(
                    labelText: 'Puerto',
                    isDense: true,
                    contentPadding: EdgeInsets.symmetric(vertical: 8, horizontal: 8),
                    border: OutlineInputBorder(),
                  ),
                  controller: TextEditingController(text: _serverPort),
                  onChanged: (val) => _serverPort = val,
                ),
              ),
              const SizedBox(width: 8),
              ElevatedButton(
                onPressed: _refreshData,
                child: const Text('Conectar'),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            justifyAxisAlignment: SystemMouseCursors.none == null ? MainAxisAlignment.spaceBetween : MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  const Text('Estado API: ', style: TextStyle(fontSize: 12, color: Colors.grey)),
                  Text(
                    _statusMessage, 
                    style: TextStyle(fontSize: 12, fontWeight: SystemMouseCursors.none == null ? FontWeight.bold : FontWeight.bold, color: _statusColor)
                  ),
                ],
              ),
              Text(
                'Capital: \$$_capital | Riesgo: $_risk%', 
                style: const TextStyle(fontSize: 12, color: Colors.grey)
              ),
            ],
          )
        ],
      ),
    );
  }

  Widget _buildSignalCard(dynamic signal) {
    final bool isBuy = signal['type'] == 0; // SignalType.BUY
    final String typeText = isBuy ? 'COMPRA (BUY)' : 'VENTA (SELL)';
    final Color typeColor = isBuy ? Colors.blue : Colors.redAccent;
    final String symbol = signal['symbol'] ?? 'EUR/USD';
    final String timeframe = signal['timeframe'] ?? '15m';
    
    final String entry = (signal['entryPrice'] as num).toStringAsFixed(5);
    final String sl = (signal['stopLoss'] as num).toStringAsFixed(5);
    final String tp1 = (signal['takeProfit1'] as num).toStringAsFixed(5);
    final String tp2 = (signal['takeProfit2'] as num).toStringAsFixed(5);
    final String tp3 = (signal['takeProfit3'] as num).toStringAsFixed(5);
    final String lotes = (signal['positionSize'] as num).toStringAsFixed(2);
    final String riskUsd = (signal['riskAmount'] as num).toStringAsFixed(2);

    final String generatedTime = _formatToLocalTime(signal['createdAt']);
    final String winStart = _formatToLocalTime(signal['entryWindowStart']);
    final String winEnd = _formatToLocalTime(signal['entryWindowEnd']);

    return Card(
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: typeColor.withOpacity(0.3), width: 1),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Cabecera de la señal
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: typeColor.withOpacity(0.15),
                        borderRadius: BorderRadius.circular(6),
                        border: BorderSide(color: typeColor.withOpacity(0.3)),
                      ),
                      child: Text(
                        typeText,
                        style: TextStyle(color: typeColor, fontWeight: FontWeight.bold, fontSize: 11),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Text(
                      symbol,
                      style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                    ),
                    const SizedBox(width: 6),
                    Text(
                      timeframe,
                      style: const TextStyle(color: Colors.grey, fontSize: 12),
                    ),
                  ],
                ),
                Text(
                  'Hora: $generatedTime',
                  style: const TextStyle(color: Colors.grey, fontSize: 11, fontFamily: 'monospace'),
                ),
              ],
            ),
            const Divider(height: 20, color: Color(0xFF334155)),

            // Parámetros copiables
            _buildCopyableRow('Precio Entrada', entry),
            _buildCopyableRow('🛡️ Stop Loss (SL)', sl, isDanger: true),
            _buildCopyableRow('🎯 Take Profit 1 (TP1)', tp1, isSuccess: true),
            _buildCopyableRow('🎯 Take Profit 2 (TP2)', tp2, isSuccess: true),
            _buildCopyableRow('🎯 Take Profit 3 (TP3)', tp3, isSuccess: true),
            _buildCopyableRow('💰 Posición (Lotes)', lotes, riskAmount: riskUsd),
            
            // Ventana horaria de entrada
            Container(
              margin: const EdgeInsets.only(top: 8, bottom: 8),
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: const Color(0xFF0F172A).withOpacity(0.5),
                borderRadius: BorderRadius.circular(6),
                border: BorderSide(color: const Color(0xFF334155).withOpacity(0.5)),
              ),
              child: Row(
                children: [
                  const Icon(Icons.access_time, size: 14, color: Colors.amberAccent),
                  const SizedBox(width: 6),
                  Text(
                    'Ventana de Entrada Local: $winStart - $winEnd',
                    style: const TextStyle(fontSize: 10, color: Colors.grey, fontFamily: 'monospace'),
                  ),
                ],
              ),
            ),
            
            // Justificación y Explicación IA en colapsable
            Theme(
              data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
              child: ExpansionTile(
                title: const Text(
                  'Detalles del Análisis Técnico e IA',
                  style: TextStyle(fontSize: 12, color: Colors.indigoAccent, fontWeight: FontWeight.bold),
                ),
                tilePadding: EdgeInsets.zero,
                children: [
                  Padding(
                    padding: const EdgeInsets.only(top: 4, bottom: 8),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Justificación: ${signal['technicalJustification']}',
                          style: const TextStyle(fontSize: 11, color: Colors.grey),
                        ),
                        if (signal['iaExplanation'] != null) ...[
                          const SizedBox(height: 8),
                          Container(
                            padding: const EdgeInsets.all(10),
                            decoration: BoxDecoration(
                              color: const Color(0xFF0F172A),
                              borderRadius: BorderRadius.circular(8),
                              border: BorderSide(color: const Color(0xFF334155)),
                            ),
                            child: Text(
                              signal['iaExplanation'],
                              style: const TextStyle(fontSize: 11, color: Colors.grey, fontStyle: FontStyle.italic),
                            ),
                          ),
                        ]
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildCopyableRow(String label, String value, {bool isDanger = false, bool isSuccess = false, String? riskAmount}) {
    Color valColor = Colors.grey[300]!;
    if (isDanger) valColor = Colors.redAccent;
    if (isSuccess) valColor = Colors.greenAccent;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              Text(label, style: const TextStyle(fontSize: 13, color: Colors.grey)),
              if (riskAmount != null)
                Text(' (Riesgo: \$$riskAmount USD)', style: const TextStyle(fontSize: 11, color: Colors.grey)),
            ],
          ),
          Row(
            children: [
              Text(
                value,
                style: TextStyle(fontFamily: 'monospace', fontWeight: FontWeight.bold, fontSize: 14, color: valColor),
              ),
              const SizedBox(width: 8),
              IconButton(
                constraints: const BoxConstraints(maxHeight: 30, maxWidth: 30),
                padding: EdgeInsets.zero,
                icon: const Icon(Icons.copy, size: 16, color: Colors.grey),
                onPressed: () => _copyToClipboard(label, value),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
