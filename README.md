MakeItRobotics
==============

.NETMF port of the software for the [Make It Robotics Kit from Radio Shack](http://blog.radioshack.com/2014/05/radioshack-make-magazine-launch-make-robotics/).

### History

I bought this kit as a father/son activity. I had several FEZ Panda II's sitting idle from an old project, and wasn't keen on putting down $30 for a meh microcontroller board (the Arduino Uno Rev 3). Since the Panda II is hardware compatible with an Uno, I only needed to worry about the software.

I ported [the "line following" project from the official source code](http://blog.radioshack.com/wp-content/uploads/2014/05/Make-it-Robotics-Starter-Kit-Support-Files.zip), which is a combination of a big C++ class and an Arduino Sketch. It's now just a single C# class. I did not conform the code style to standard C# style in order to preserve the ability to refer back to the C++/Sketch code. This will come in handy to anyone wanting to port over the code for the "walk" build, or any of the add-on packs (let alone community mods).

I also found that I had to make a couple of changes to the wiring in order to get it work right. That seems to be a common issue even among Uno users, so YMMV.

It's a great kit, especially with a FEZ Panda II. ;)
