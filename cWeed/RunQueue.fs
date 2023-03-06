module RunQueue

// queue to store transactions that need to run this minute

// function to handle spinning up threads to process them
// needs some means to know when the proc ends so a new one
// can be spun up in its place to process then next transaction