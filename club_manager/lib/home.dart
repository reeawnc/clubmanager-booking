//import 'dart:async';
//import 'dart:convert';

//import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
//import 'package:http/http.dart' as http;

/*
import 'package:squash_manager/ui/Container(),//.dart';
import 'package:squash_manager/ui/box_league/box_listing.dart';
import 'package:squash_manager/ui/dashboard/dashboard.dart';
import 'package:squash_manager/models/root_object.dart';
*/
class HomeScreen extends StatefulWidget {
  const HomeScreen({Key? key}) : super(key: key);
  @override
  State createState() => MainWidgetState();
}

class MainWidgetState extends State<HomeScreen> {
  //DateTime _date = new DateTime.now();
  //RootObject _data = null;

  @override
  void initState() {
    super.initState();
/*
    fetchCourts(http.Client(), _date).then((result) {
      setState(() {
        _data = result;
      });
    });
    */
  }
/*
  Future<Null> _selectDate(BuildContext context) async {
    final DateTime picked = await showDatePicker(
        context: context,
        initialDate: _date,
        firstDate: new DateTime(2016),
        lastDate: new DateTime(2019));

    if (picked != null && picked != _date) {
      _date = picked;
      final RootObject data = await fetchCourts(http.Client(), _date);
      print('Date selected: ${_date.toString()}');
      setState(() {
        _date = picked;
        _data = data;
      });
    }
  }
  */
/*
  Future<RootObject> fetchCourts(http.Client client, DateTime date) async {
    String apiUrl =
        "https://squashmanager.azurewebsites.net/api/Courts?code=rfYtvUWQGIb1aiRcAW1bQBijxSLdPDtIlL5zEVbqMAH9MX2wUm6dJw==";
    final response =
        await client.get(apiUrl, headers: {'date': date.toString()});
    var res = parseCourts(response.body);
    return res;
  }
  */
/*
  // A function that will convert a response body into a List<Photo>
  RootObject parseCourts(var contentBody) {
    dynamic test = json.decode(contentBody);
    return RootObject.fromJson(test);
  }
  */

  Widget build(BuildContext context) {
    return Scaffold(
      drawer: Drawer(
        // Add a ListView to the drawer. This ensures the user can scroll
        // through the options in the Drawer if there isn't enough vertical
        // space to fit everything.
        child: ListView(
          // Important: Remove any padding from the ListView.
          padding: EdgeInsets.zero,
          children: <Widget>[
            const DrawerHeader(
              child: Text('Drawer Header'),
              decoration: BoxDecoration(
                color: Colors.blue,
              ),
            ),
            ListTile(
              title: const Text('Courts'),
              onTap: () {
                // Update the state of the app
                // ...
                // Then close the drawer
                /*Navigator.push(
                  context,
                  MaterialPageRoute(builder: (context) => Courts(_data)),
                );*/
              },
            ),
            ListTile(
              title: const Text('Box League'),
              onTap: () {
                // Update the state of the app
                // ...
                // Then close the drawer
                /*
                Navigator.push(
                  context,
                  MaterialPageRoute(builder: (context) => BoxListingAlt()),
                );
                */
              },
            ),
            ListTile(
              title: const Text('Dashboard'),
              onTap: () {
                // Update the state of the app
                // ...
                // Then close the drawer
                /*Navigator.push(
                  context,
                  MaterialPageRoute(builder: (context) => Dashboard()),
                );
                */
              },
            ),
          ],
        ),
      ),
      appBar: AppBar(
        title: const Text('Westwood Club Manager'),
        actions: <Widget>[
          IconButton(
            icon: const Icon(Icons.calendar_today),
            onPressed: () {
              //_selectDate(context);
            },
          )
        ],
      ),
      body: Container(), //Courts(_data),
    );
  }
}
